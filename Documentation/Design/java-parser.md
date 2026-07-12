# Java import via a standalone parser tool

## Goal

`CSharpCodeAnalyst` is currently limited to C#. The single biggest gap in reach is Java. This
document lays out the plan to close that gap **without** touching the core application or the
`CodeGraph` model: a separate, standalone Java tool parses a Maven/Gradle project and writes a
graph file that the existing "Import plain text graph" feature already knows how to read.

Non-goal: this is not about building a generic multi-language front-end inside
`CSharpCodeAnalyst`. It is a second, independent producer of the same `CodeGraph` text format,
built and shipped separately (comparable to how `jdeps` output is already imported today via
`JdepsReader`).

## Why this is feasible with reasonable effort

`CodeElement` / `Relationship` (`CSharpCodeAnalyst.CodeGraph/Graph/`) have no dependency on
Roslyn or C#; they are plain string/enum-based. `CodeGraphSerializer`
(`CSharpCodeAnalyst.CodeGraph/Export/CodeGraphSerializer.cs`) already defines a human-readable,
round-trippable text format for the whole graph (elements with hierarchy/attributes/source
locations, plus typed relationships), and `Importer.ImportPlainTextAsync` already loads it. If the
Java tool emits this format directly, **no new C# code is needed at all** — parsing, cycle
detection, exporters, architectural rules, and the UI all work unmodified.

This also means the Java side can aim for compiler-grade precision instead of falling back to
heuristics. Unlike most cross-language codebase visualizers (e.g. depwire, which uses tree-sitter
and pattern/import matching, not real type resolution), Java has a mature library that does actual
semantic symbol resolution comparable to Roslyn's `SemanticModel`:
[JavaParser](https://github.com/javaparser/javaparser) with its bundled Symbol Solver
(`javaparser-symbol-solver-core`, merged into the main JavaParser repo since 3.5.10). If further
languages are added later, they don't need to follow this pattern — a tree-sitter-based, heuristic
importer is a reasonable fallback for languages without an equivalent solver library, and would
plug into the same text-format import path.

## Mapping Java concepts onto the existing graph model

| Java construct | `CodeElementType` |
|---|---|
| Maven module / Gradle subproject / JAR | `Assembly` |
| package | `Namespace` |
| `class` | `Class` |
| `interface` | `Interface` |
| `enum` | `Enum` |
| `record` (Java 16+) | `Record` |
| method | `Method` |
| field | `Field` |

`Property`, `PropertyAccessor`, `Delegate`, `Event`, `Struct` have no Java equivalent and are
simply never emitted.

| Java construct | `RelationshipType` |
|---|---|
| method call | `Calls` |
| `new Foo()` | `Creates` |
| type usage (parameter, return type, field type, local variable) | `Uses` |
| `extends` (class) | `Inherits` |
| `implements` / `extends` (interface) | `Implements` |
| `@Override` | `Overrides` |
| annotation usage | `UsesAttribute` |

`Containment` does not need to be emitted explicitly — it is derived automatically from the
`parent=` field on each element line. `Invokes` / `Handles` (C# event registration) and `Bundled`
(UI-only aggregate edges) have no Java counterpart and are not used.

Types that cannot be resolved to project source (JDK classes, third-party JARs without attached
source) are emitted with the `external` flag, as leaf nodes — the same convention already used for
NuGet/framework references in the C# parser.

## Tool architecture

A standalone Java console application, developed and versioned separately from the .NET solution
(own repo or own top-level folder, not part of `CSharpCodeAnalyst.sln`):

1. **Input**: path to a Maven or Gradle project root.
2. **Classpath resolution**: shell out to the project's own build tool to get a real, resolved
   classpath (see below) — this is the Java-side equivalent of `MSBuildWorkspace` opening a
   solution.
3. **Parsing & symbol resolution**: JavaParser + `JavaSymbolSolver`, backed by a
   `CombinedTypeSolver` made of one `JavaParserTypeSolver` per source root, one `JarTypeSolver` per
   resolved dependency jar, and a `ReflectionTypeSolver` for the JDK itself.
4. **Two-pass extraction**, mirroring `HierarchyAnalyzer` → `RelationshipAnalyzer`: first walk all
   compilation units to build the package/type/member hierarchy, then walk method bodies and
   signatures to resolve and emit relationships.
5. **Output**: write directly in the `CodeGraphSerializer` text format described above. No JSON
   intermediate, no C#-side DTO mapping.
6. **Deployment**: packaged as a single (shaded/fat) JAR. Requires a JRE on the user's machine,
   same assumption already made for the existing `jdeps` import path.

## Known risk areas (researched up front)

**JavaSymbolSolver maturity.** Actively maintained as part of the main JavaParser repo (releases
roughly every few weeks to months, latest around 3.28.x). Known rough edges:
- Java 17+ pattern matching in `switch` (record patterns, guarded patterns) is not fully modeled
  as first-class AST nodes yet ([issue #4361](https://github.com/javaparser/javaparser/issues/4361)).
- Symbol resolution on `record` types has reported failures in some configurations
  ([issue #4758](https://github.com/javaparser/javaparser/issues/4758)).
- Nested classes and generics are a recurring source of `UnsolvedSymbolException`
  ([#2366](https://github.com/javaparser/javaparser/issues/2366),
  [#1872](https://github.com/javaparser/javaparser/issues/1872),
  [#1943](https://github.com/javaparser/javaparser/issues/1943)) — in practice these trace back to
  an incomplete `CombinedTypeSolver` (a source root or jar not registered) rather than solver bugs.
- There is no automatic "multi-module" concept: every module's source root and every module's
  build output/jar must be registered into one shared `CombinedTypeSolver` for cross-module calls
  to resolve. This is a real implementation cost, not just configuration boilerplate.

Consequence for the design: unresolved symbols must degrade gracefully (emit as `external` /
skip the edge) rather than aborting the import, mirroring how the C# parser already tolerates
unresolved references via `IParserDiagnostics`.

**Classpath extraction.** Neither Maven nor Gradle offers a purely static/offline way to get a
classpath — both require actually invoking the build tool's own dependency resolution, which can
trigger downloads if the local cache is incomplete.
- Maven: `mvn dependency:build-classpath` is the standard approach; per-module in a multi-module
  reactor (needs `-pl`/`-am` or iteration per module). A known race condition exists where a
  sibling module's classpath entry can resolve to a stale local-repo jar, `target/classes`, or a
  freshly built artifact depending on build state.
- Gradle: no built-in "print classpath" task exists (open feature request:
  [gradle/gradle#20460](https://github.com/gradle/gradle/issues/20460)); the standard approach is a
  small custom task or init script that resolves the `compileClasspath`/`runtimeClasspath`
  `Configuration` and dumps the resolved files. Gradle-wrapper version mismatches between the
  target project and the injected script are a realistic source of API breakage.

Consequence for the design: the tool should assume the target project is already buildable
(dependencies resolvable, network access or warm local cache available) and surface a clear error
rather than a partial/misleading graph if classpath resolution fails.

**Prior art.** No existing open-source tool was found that pairs JavaParser + SymbolSolver
end-to-end over a Maven/Gradle project to emit a full semantic dependency graph the way this plan
proposes. Related but different in approach: jQAssistant and Jarviz operate on compiled bytecode
(ASM) rather than source-level AST + symbol resolution; Sonargraph is a commercial, closed-source
comparable in UX terms. This looks like a genuine gap rather than reinventing an existing wheel.

## Suggested phased scope

1. **Spike**: single-module Maven project, hard-coded classpath, hierarchy + `Uses`/`Calls`/
   `Inherits`/`Implements` only, output validated by round-tripping through
   `CodeGraphSerializer.Deserialize` and opening it in the app.
2. **Classpath automation**: drive `mvn dependency:build-classpath` (Maven) and a bundled init
   script (Gradle) instead of a hard-coded classpath.
3. **Multi-module support**: build one shared `CombinedTypeSolver` across all modules in a reactor/
   settings.gradle.
4. **Polish**: `Creates`, `Overrides`, `UsesAttribute`, external-type leaf nodes, error reporting
   for unresolved symbols, packaging as a distributable JAR.

Not planned initially: Gradle version-catalog edge cases beyond the common case, annotation
processors / generated sources, and anything beyond what `CodeGraphSerializer` already expresses
(e.g. no new relationship types).
