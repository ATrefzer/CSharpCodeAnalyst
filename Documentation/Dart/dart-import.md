# Dart / Flutter import

This is the running record of how Dart is mapped onto the code graph, and why. It plays the same
role for the Dart import that `Documentation/Roslyn/corrections-and-updates.md` plays for the C#
parser: whenever the mapping changes, or a Dart construct turns out to be tricky, the decision and
its reasoning belong here.

## Why not doxygen

C++ and Python are imported by running doxygen over the sources. Doxygen does not support Dart at
all — its language list has no entry for it (the "D" it supports is the D language, not Dart). A
filter that rewrites Dart to look like Java would break on async/await, mixins, extensions, arrow
bodies and null-safety operators.

Dart ships its own analysis front end as a pub package, `analyzer`. It is the same engine the IDE
plugins use, so it gives a fully resolved element model: supertypes, call targets, constructor
invocations and type annotations, resolved across files and into the Flutter SDK. That is strictly
more than doxygen could deliver even for a supported language.

## Architecture

The import is split across a language boundary, because `analyzer` only exists for Dart:

| Part | Where | Job |
| --- | --- | --- |
| `DartExtractor/` | Dart package at the repository root | Analyzes the project, emits JSON |
| `DartRunner` | `Features/Import/` | Finds the Dart SDK, runs the extractor |
| `DartExtractorDeployment` | `Features/Import/` | Copies the tool to `%LocalAppData%` and resolves it once |
| `DartGraphConverter` | `Features/Import/` | JSON → `CodeGraph` |

The JSON carries the literal names of `CodeElementType` and `RelationshipType`, so the modelling
decisions all live on the Dart side and the converter stays a pure rebuild of the object graph.
`format` in the JSON is the contract version; the converter refuses anything else rather than
guessing.

**Why a deployment step.** Running the extractor needs `dart pub get`, which writes `.dart_tool/`
and `pubspec.lock` into the package directory. The installation directory may be read-only, so the
shipped sources are copied to `%LocalAppData%\CSharpCodeAnalyst\DartExtractor\<fingerprint>` and
resolved there. The fingerprint is a hash over the shipped files rather than the application
version — during development the sources change while the version does not, and a stale resolved
copy would otherwise be used forever.

**Why dart.exe and not dart.** What sits on the PATH is usually Flutter's `dart.bat` wrapper, which
cannot be started without a shell. `DartRunner` therefore prefers a real `dart.exe`, falling back to
`<flutterRoot>\bin\cache\dart-sdk\bin\dart.exe`.

**Licensing follows from this, and only from this.** We ship our own Dart sources; `analyzer`, `path`
and `yaml` are fetched by `pub` into the user's own cache. That is not redistribution, so their
BSD-3-Clause terms are not triggered and they need no entry in `ThirdPartyNotices/` — the same reason
doxygen, which the C++/Python import depends on just as hard, has none either. **If the tool is ever
switched to `dart compile exe`, this changes:** the packages would be compiled into a binary we ship,
and then both `ThirdPartyNotices/` and the README acknowledgement become mandatory.

## Hierarchy: Dart has no namespaces

Dart organizes code by package and by file; there is no namespace construct. The hierarchy is
therefore derived:

- **Assembly = package.** `package:app/...` becomes the assembly `app`, `dart:core` becomes the
  assembly `dart:core`. Files outside `lib/` (`test/`, `bin/`, `tool/`) are not package-addressable
  and have a `file:` URI; their package name comes from the pubspec of the enclosing analysis
  context root.
- **Namespace = the path inside the package, including the library file.**
  `package:app/features/auth/login_page.dart` becomes `features` → `auth` → `login_page`. This
  mirrors the Python import, where doxygen maps `pkg/mod.py` to the namespace `pkg::mod`.
- A library with no path segments of its own lands in the synthetic `global` namespace, following
  the convention of the C# parser. No element ever sits directly below an assembly.
- **`part` files are folded into their library.** They have no library of their own, and Dart's
  privacy model treats a library and its parts as one unit, so a separate namespace would be wrong.

## External code

Everything outside the analyzed directory — the Dart/Flutter SDK and pub packages — is created on
demand with its full parent chain and marked `IsExternal`, exactly the way `ExternalCodeElementCache`
does it on the C# side. Members of external types are created too, so a call to `State.setState` is
visible as such rather than collapsing onto the type.

## Element mapping

| Dart | `CodeElementType` | Note |
| --- | --- | --- |
| `class` | `Class` | |
| `interface class` / `abstract interface class` | `Interface` | A plain `abstract class` stays a `Class` — it can carry implementation |
| `mixin` | `Class` | It has implementation and is part of the superclass chain; `Interface` would be misleading |
| `extension` | `Class` | A named container of methods. Unnamed extensions are dropped: they cannot be referenced by name and would produce anonymous nodes |
| `extension type` | `Struct` | A zero-cost wrapper over a representation type |
| `enum` | `Enum` | |
| `typedef` | `Delegate` | Plus a `Uses` edge to the aliased type |
| top-level function, method, constructor | `Method` | The unnamed constructor is called `new`, matching Dart's own `MyApp.new` syntax |
| field, top-level variable, enum constant | `Field` | |
| getter, setter | `Property` | A getter/setter pair shares one element, like the C# default |

Ids are `<library uri>#<qualified name>`, e.g. `package:app/main.dart#MyApp.build`. Dart has no
overloading, so a name is unique inside its container.

**Synthetic accessors.** Reading `obj.count` on a field resolves to the compiler-generated getter,
not to the field. Those synthetic accessors are redirected to their variable, so the edge points at
the declared field.

## Relationship mapping

| Dart | `RelationshipType` | Note |
| --- | --- | --- |
| `extends` | `Inherits` | The implicit `extends Object` is dropped — it carries no information |
| `with` (mixin application) | `Inherits` | A mixin application is part of the superclass chain in Dart |
| `implements` | `Implements` | |
| `on` (mixin constraint) | `Uses` | A constraint on the user of the mixin, not an inheritance |
| method invocation | `Calls` | |
| constructor invocation | `Creates` | The edge points at the constructor, which lives below the created type, so a type-level rollup still yields "creator → created type" |
| getter/setter access | `Calls` | They are executable; field and variable reads are `Uses` |
| tear-off (`onPressed: _increment`) | `Uses` | References a method without calling it |
| type annotations, type arguments | `Uses` | `Future<List<Order>>` reaches `Order`, not only `Future` |
| annotation | `UsesAttribute` | Resolved to the annotation type where possible |
| method override | `Overrides` | |

**Closures downgrade calls to `Uses`.** A closure body is not executed where it is written, so
everything inside one is recorded as `Uses` instead of `Calls`. This mirrors how the C# parser
treats lambda bodies (see `ISyntaxNodeHandler`). It matters far more in Dart than in C#: Flutter
code is largely builder callbacks, and treating them as unconditional calls would make almost every
widget tree look like a call chain.

**Constructors do not use their own class.** A constructor's return type is its class, which would
be an edge from a child to its own parent. The return type is skipped for constructors.

**Supertype clauses are not also `Uses`.** `extends`/`implements`/`with`/`on` are modelled from the
element model. The type names in those clauses are skipped during the AST walk, otherwise every
inheritance edge would be shadowed by a `Uses` edge. Type *arguments* inside such a clause are not
skipped — `extends State<MyHomePage>` legitimately uses `MyHomePage`.

## Source metrics

Members with a body get the same four metrics the C# parser collects, and the counting rules are
deliberately identical so the numbers mean the same thing in both languages — see
`SourceMetricsCollector` for the C# side and `metrics_collector.dart` for the Dart side:

| Metric | Rule |
| --- | --- |
| `CodeLines` | Physical lines touched by a real token. A line with code and a trailing comment counts as code |
| `CommentLines` | Comment-only lines, including the documentation comment above the signature |
| `LogicalLinesOfCode` | Executable statements, block wrappers excluded; an expression body (`=> x * 2`) counts as one |
| `CyclomaticComplexity` | McCabe: one plus the decision points |

Decision points are `if`, `while`, `do`, `for`, `case`, `catch`, `? :`, `&&`, `||`, `??` and `??=` —
the same set as in C#, plus the two Dart constructs with no C# equivalent: `if` and `for` inside a
collection literal are real branches. A `default:` label is not counted, and neither is a bare `_ =>`
arm in a switch expression, which is its equivalent; a guarded `_ when ... =>` is.

Only members with a body are measured — abstract and external declarations would report a body of
zero lines and dilute every average. The metrics are emitted in a map keyed by element id next to
the graph, mirroring how `MetricStore` sits beside the `CodeGraph` rather than on its elements.

**Where the metrics are computed.** In `ReferenceVisitor`, not in `DartExtractor`. The extractor
declares elements from the element model, which has no syntax attached; the visitor is already
positioned at each declaration's AST node and knows the element it belongs to.

**The `beginToken` trap.** `AstNode.beginToken` of a declaration carrying a `///` documentation
comment returns the *comment's* token. Comment tokens live in the `precedingComments` chain, not in
the main token stream, so following `next` from there leaves the declaration immediately and the
whole body goes uncounted. `MetricsCollector._firstTokenInStream` therefore starts at the first
annotation, or at `firstTokenAfterCommentAndMetadata`. Annotations are regular tokens and do count
as code.

## Known limitations

- **Dynamic dispatch does not resolve.** A call on a `dynamic` receiver binds to no element and is
  counted as unresolved rather than guessed. On Flutter's own `examples/api` this affects about 1%
  of all references.
- **Operators from `dart:core` produce edges.** In Dart every operator is a method, so `a + b` on
  `int` yields an edge to `num.+`. This is honest but noisier than the C# parser, which can ignore
  built-in operators because they bind to no user-defined method.
- **Generated code is not filtered.** `*.g.dart` and `*.freezed.dart` are analyzed like any other
  source. There is no equivalent of the C# parser's "include generated code" option yet.

## Testing

`DartGraphConverterTests` covers the converter with an inline JSON sample and needs no Dart SDK.
`DartImportEndToEndTests` runs the whole chain against a real project; it is `[Explicit]` because it
needs a Dart SDK and a resolved project. Point it at one with the `CSCA_DART_TEST_PROJECT`
environment variable.
