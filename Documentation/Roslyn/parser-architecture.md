# Parser Architecture Overview

This document gives a simple overview of how `CSharpCodeAnalyst.CodeParser` converts a C# solution into a `CodeGraph`. There are two related documents:

- [roslyn-guide.md](roslyn-guide.md): covers Roslyn concepts like `GetSymbolInfo` vs `GetDeclaredSymbol`, and symbols vs syntax.
- [corrections-and-updates.md](corrections-and-updates.md): a running log of *modelling* decisions.
It explains which C# construct maps to which edge, and why. Check that document if you want to know why the graph looks the way it does. Use this document to see where to make changes.

## The pipeline

`Parser.ParseSolutionInternal` is the whole program. Five steps, strictly in order:

```
        MSBuildWorkspace  /  AdhocWorkspace
                    │
                    ▼  Solution (Roslyn)
  1. HierarchyAnalyzer.BuildHierarchy   →  CodeGraph (nodes only) + Artifacts
  2. CollectSourceMetrics               →  MetricStore
  3. RelationshipAnalyzer               →  the same nodes, now with edges
  4. XamlGraphLinker         (optional) →  what Roslyn cannot see
  5. InsertGlobalNamespaceIfUsed        → post-normalization
                    │
                    ▼
              ParseResult
```

Three entry points feed the same pipe:

| Input | Workspace | Note |
| --- | --- | --- |
| `.sln` / `.slnx` | `MSBuildWorkspace` | needs `Initializer.InitializeMsBuildLocator()` first |
| `.csproj` | `MSBuildWorkspace` | single project, wrapped in its own solution |
| `.cs` | `AdhocWorkspace` | reads the file, then goes through the in-memory path |
| a code string (`ParseSourceAsync`) | `AdhocWorkspace` | no MSBuild, no disk access.<br />Entry point for the in-memory unit tests. Executes the full pipeline! A synthetic  `InMemory.csproj` project is created. |

## Core Execution Model: The Two-Pass Design

- **Phase 1 creates only nodes.** It never looks into a method body.
- **Phase 2 creates only edges.** It (almost) never creates a node.

There are some exceptions to this two-pass design.

| Exception | Why |
| --- | --- |
| `GlobalStatements` / `Execute` — phase 2 creates nodes | Top-level statements belong to no type and no method, so a synthetic class/method per assembly hosts their dependencies.<br /><br />Also, a synthetic "global" namespace is introduced at the end if at least one Assembly has code elements without a namespace defined. This simplifies later cycle search. |
| External elements — phase 2 creates nodes | You only learn that the external class `List<T>` is needed while resolving an edge. Collected in `ExternalCodeElementCache` and flushed into the graph *after* the parallel loop. |
| Primary constructors and captured parameters | Handled during Phase 1 by inspecting symbol models alongside syntax. See [corrections-and-updates.md](corrections-and-updates.md). |

## Phase 1 — Hierarchy Walk

`HierarchyAnalyzer.ProcessNodeForHierarchy(node, semanticModel, parent)` is a plain recursive descent over the syntax tree with one large `switch`:

```
symbol = GetDeclaredSymbol(node)          // per syntax kind
    │
    ├── symbol != null  →  element = GetOrCreateCodeElement(...)
    │                      recurse into children with element as parent
    │
    └── symbol == null  →  recurse into children with the SAME parent
```

The `null` branch is more important than it looks: **a syntax node without an element is skipped, not
cut off.** 

Two things happen after the element is created: property accessors are split off when configured
(`SplitPropertyAccessors`), and a type declaration gets its primary constructor plus any captured
parameters.

Source-generated documents are walked too. What is generated is *marked*
instead (`MarkGeneratedElements`, which runs at the very end because the decision needs all declarations
of a partial type). For example, `[ObservableProperty]` backing fields.

The walk fills five collections, handed to phase 2 as `Artifacts` (read-only from then on):

| Artifact | What phase 2 needs it for |
| --- | --- |
| `SymbolKeyToElementMap` | **the core** — symbol → element |
| `ElementIdToSymbolMap` | the way back: phase 2 iterates elements and needs their symbol |
| `AllNamedTypesInSolution` | finding implementers |
| `InterfaceImplementations` | precomputed interface → implementing types, so phase 2 is O(1) per member instead of scanning every type |
| `GlobalStatementsByAssembly` | the homeless top-level statements |

## `Key()` — the bridge between Roslyn and the graph

Roslyn symbols are **not** identical across compilations: the same method seen from two projects is two
instances, and `SymbolEqualityComparer` does not help. The parser solves this with its own string
key (`SymbolExtensions.Key`), built from the whole chain down from the assembly:

```
.ctor_Demo.ILogger_Method.Service_NamedType.Demo_Namespace._Namespace.MyAssembly
└name┘└ parameters ┘└kind┘└  type  ┘        └ namespace ┘        └  assembly  ┘
```

The price: phase 1 and phase 2 must produce the *same* key for the same thing. Hence the many
`NormalizeToOriginalDefinition()` calls. Phase 1 sees `Cache<T>`; a call site writes `Cache<int>`;
without normalization, the two never meet.

## Phase 2 — four roles

```
RelationshipAnalyzer     orchestration: Parallel.ForEach, progress, global statements
        │
        ▼
DeclarationAnalyzer      what an element needs through its DECLARATION
        │                (base type, parameter types, return type, attributes, overrides)
        ▼
MethodBodyWalker  /  LambdaBodyWalker        traversal of BODIES
        │  ISyntaxNodeHandler (16 methods)
        ▼
SyntaxNodeAnalyzer       what a syntax node means
        │
        ▼
RelationshipBuilder      symbol resolution + the only write access to the graph
```

The separation is what keeps this navigable, and one rule carries it: **only `RelationshipBuilder`
writes to the graph**, and it holds the single lock. Phase 2 runs `Parallel.ForEach` over all elements.

`DeclarationAnalyzer.Analyze` is a type switch over the element's symbol — event, delegate, named type,
method, property, field, parameter (a captured primary constructor parameter) — plus attributes for all
of them.

## The resolution cascade

This is what shapes what the tool shows.
`RelationshipBuilder.AddRelationshipWithFallbackToContainingType` tries, in order:

```
1. symbol found directly in the map?             → edge to that element
2. normalised (Cache<int> → Cache<T>)?           → edge to that element
3. its containing type in the map?               → edge to the TYPE
4. IncludeExternals? create an external element  → edge, always as "Uses"
```

Step 3 is the quiet coarsening you see in the UI later for external code: `myList.Add(5)` becomes `Uses List<T>`, not `Calls List<T>.Add`. Granularity is lost, the dependency never is. Before positional record properties existed, `order.Id` fell back to `Order` rather than disappearing.

## Calls vs Uses: two walkers, one base

| | `MethodBodyWalker` | `LambdaBodyWalker` |
| --- | --- | --- |
| identifier | `Calls` | `Uses` |
| `new Foo()` | `Creates` | `Uses` |
| event invoke detection | yes | no |
| nested lambda | spawns a `LambdaBodyWalker` | stays in `Uses` semantics |

The reason is semantic, not technical: a lambda body does not run where it is written, so `Calls` would
assert a control-flow edge that does not exist. `Uses` is the honest relationship — a real compile-time
dependency without claiming a call.

The source element stays the containing member in both cases, so a lambda's dependencies are attributed to the method that defines it.

## Post-processing

**XAML** (`config.IncludeXamlReferences`). Roslyn does not see what markup references. `XamlFileLocator`
answers which XAML files a project owns — `MSBuildWorkspace` does not expose the `Page` items — and
`XamlGraphLinker` adds the edges. It runs *before* the global namespace is inserted, so its synthetic
elements are moved along with everything else.

**Global namespace.** If any assembly holds types directly at the root (a test assembly with a generated
`Main`, top-level statements), a synthetic `global` namespace is inserted below *every* assembly and the
root children are moved into it. The invariant it buys: no element ever sits directly under an assembly,
so cycle detection always finds a shared ancestor at namespace level rather than at assembly level.
Anything that takes a path from the user (architectural rules) must tolerate its absence — see
`CodeElement.GlobalNamespaceName`.

## What to update on code changes

**Always** record the modelling decision in [corrections-and-updates.md](corrections-and-updates.md) —
that file lets anyone later tell a deliberate choice from an oversight.

Tests: prefer an in-memory fixture (`InMemoryParseTestBase` or `ParseSourceAsync` directly) over the
`TestSuite/` approval fixtures. The approval tests remain the safety net for Roslyn upgrades, but a new
construct is pinned far more legibly by a snippet next to its assertion.

## Known complexity hot spots

Stated plainly, because the parser is large enough that "it is complicated" stops being useful.

**Essential, and not worth cutting.** `SyntaxNodeAnalyzer` (765 lines, 16 handlers) and
`DeclarationAnalyzer` (731 lines, 14 handlers) together are half the parser. But they are *tables* — one
method per C# construct, and C# has query syntax, deconstruction, pattern matching, user-defined
operators, implicit conversions, `stackalloc`, indexers, extension methods. That grows linearly with the
language, and each method stands on its own.

**The real hurdle: five write entry points on `RelationshipBuilder`** — `AddRelationship`,
`AddTypeRelationship`, `AddCallsRelationship`, `AddSynthesizedCallRelationship`,
`AddRelationshipWithFallbackToContainingType` — with *subtly different* normalisation and fallback
rules. `AddCallsRelationship` reduces extension methods and normalizes only when the direct lookup
fails; `AddSynthesizedCallRelationship` does nearly but not the same; `AddTypeRelationship` has
its own switch over array/pointer/dynamic and recurses into itself. Nothing states which one a new case
should use. This is accidental complexity and would shrink to one entry point with an explicit
resolution strategy.

**Second: `HierarchyAnalyzer` has grown five jobs** (project selection, the hierarchy walk, symbol-key
bookkeeping, generated-code marking, the interface index) in 766 lines. The walk itself is small; the
surroundings accumulated.

Neither is a structural problem. The two-phase split and "only the builder writes" hold, and fitting
primary constructors in afterward required bending neither. This is the best available evidence that the foundation is sound.
