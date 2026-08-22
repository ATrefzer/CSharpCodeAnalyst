# Parser architecture

How `CSharpCodeAnalyst.CodeParser` turns a solution into a `CodeGraph`. This is the overview; the two
neighbouring documents cover the other halves:

- [roslyn-guide.md](roslyn-guide.md) — the Roslyn concepts themselves (`GetSymbolInfo` vs
  `GetDeclaredSymbol`, symbols vs syntax).
- [corrections-and-updates.md](corrections-and-updates.md) — the running log of *modelling* decisions:
  which C# construct is mapped to which edge, and why. Read that one when the question is "why does the
  graph look like this"; read this one when the question is "where do I change it".

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
  5. InsertGlobalNamespaceIfUsed        →  post-normalisation
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
| a code string (`ParseSourceAsync`) | `AdhocWorkspace` | no MSBuild, no disk access |

`ParseSourceAsync` matters beyond testing: it is the *same* parser, not a stand-in, so an in-memory unit
test pins real behaviour. Its synthetic project is deliberately named `InMemory.csproj` — the extension
is required to pass `ShouldAnalyzeProject`, and the fixed name keeps a user exclusion filter like
`.*Tests` from silently dropping a snippet in a file called `FooTests.cs`.

## The load-bearing principle: two passes

An edge `A → B` can only be written once **both** elements exist. Building nodes and edges in one walk
would mean buffering forward references and resolving them later — two passes again, just interleaved.
So instead:

- **Phase 1 creates only nodes.** It never looks into a method body.
- **Phase 2 creates only edges.** It (almost) never creates a node.

That rule is the map. Nearly every surprise in the parser is an exception to it, and there are only
three:

| Exception | Why |
| --- | --- |
| `GlobalStatements` / `Execute` — phase 2 creates nodes | Top-level statements belong to no type and no method, so a synthetic class/method per assembly hosts their dependencies. |
| External elements — phase 2 creates nodes | You only learn that `List<T>` is needed while resolving an edge. Collected in `ExternalCodeElementCache` and flushed into the graph *after* the parallel loop. |
| Primary constructors and captured parameters | Phase 1 has to ask the symbol model rather than the syntax — the declaration is the type declaration. See [corrections-and-updates.md](corrections-and-updates.md). |

## Phase 1 — one recursive walk, five side collections

`HierarchyAnalyzer.ProcessNodeForHierarchy(node, semanticModel, parent)` is a plain recursive descent
over the syntax tree with one large `switch`:

```
symbol = GetDeclaredSymbol(node)          // per syntax kind
    │
    ├── symbol != null  →  element = GetOrCreateCodeElement(...)
    │                      recurse into children with element as parent
    │
    └── symbol == null  →  recurse into children with the SAME parent
```

The `null` branch is more important than it looks: **a syntax node without an element is skipped, not
cut off.** That is why adding positional record properties needed a single `case ParameterSyntax` —
those parameters were already being visited, they just produced nothing.

Two things happen after the element is created: property accessors are split off when configured
(`SplitPropertyAccessors`), and a type declaration gets its primary constructor plus any captured
parameters.

Source-generated documents are walked too, and always. Leaving them out would not merely hide generated
members — it would remove the only reference many hand-written ones have (nothing else reads an
`[ObservableProperty]` backing field), turning them into false dead code. What is generated is *marked*
instead (`MarkGeneratedElements`, which runs at the very end because the decision needs all declarations
of a partial type).

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
instances, and `SymbolEqualityComparer` does not help. The parser sidesteps this with its own string
key (`SymbolExtensions.Key`), built from the whole chain down from the assembly:

```
.ctor_Demo.ILogger_Method.Service_NamedType.Demo_Namespace._Namespace.MyAssembly
└name┘└ parameters ┘└kind┘└  type  ┘        └ namespace ┘        └  assembly  ┘
```

This is why `FindInternalCodeElement(symbol)` is a plain dictionary lookup, and why adding a new kind of
element is cheap — the key falls out of the existing path.

The price: phase 1 and phase 2 must produce the *same* key for the same thing. Hence the many
`NormalizeToOriginalDefinition()` calls. Phase 1 sees `Cache<T>`; a call site writes `Cache<int>`;
without normalisation the two never meet.

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
writes to the graph**, and it holds the single lock. Phase 2 runs `Parallel.ForEach` over all elements
and an edge frequently targets a foreign element, so without that funnel it would be a field of data
races.

`DeclarationAnalyzer.Analyze` is a type switch over the element's symbol — event, delegate, named type,
method, property, field, parameter (a captured primary constructor parameter) — plus attributes for all
of them.

## The resolution cascade

This is the part that shapes what the tool shows.
`RelationshipBuilder.AddRelationshipWithFallbackToContainingType` tries, in order:

```
1. symbol found directly in the map?             → edge to that element
2. normalised (Cache<int> → Cache<T>)?           → edge to that element
3. its containing type in the map?               → edge to the TYPE
4. IncludeExternals? create an external element  → edge, always as "Uses"
```

Step 3 is the quiet coarsening you see in the UI later: `myList.Add(5)` becomes `Uses List<T>`, not
`Calls List<T>.Add`. Granularity is lost, the dependency never is. It is also why missing elements stay
invisible for a long time — before positional record properties existed, `order.Id` fell back to `Order`
rather than disappearing.

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

Mechanically this is a virtual `MemberReferenceType` on `SyntaxWalkerBase` plus overridden visits (24
overrides in the base, 13 and 9 in the two subclasses). Both walkers talk to the same
`SyntaxNodeAnalyzer` through `ISyntaxNodeHandler`; the source element stays the containing member in
both cases, so a lambda's dependencies are attributed to the method that defines it.

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

## Where to make which change

| Task | Where |
| --- | --- |
| A C# construct produces no edge | `SyntaxNodeAnalyzer` (a handler method) + the matching `Visit` override in the walker base or `MethodBodyWalker` |
| A declaration-level dependency is missing (parameter, base type, constraint) | `DeclarationAnalyzer` |
| A construct produces no code *element* | `HierarchyAnalyzer.ProcessNodeForHierarchy` — a `case` in the switch |
| Resolution goes to the wrong target | `RelationshipBuilder`, the cascade above |
| A new kind of element | `CodeElementType` + phase 1 case; the key comes for free |

**Always** record the modelling decision in [corrections-and-updates.md](corrections-and-updates.md) —
that file is the reason anyone can later tell a deliberate choice from an oversight.

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
rules. `AddCallsRelationship` reduces extension methods and normalises only when the direct lookup
fails; `AddSynthesizedCallRelationship` does nearly but not exactly the same; `AddTypeRelationship` has
its own switch over array/pointer/dynamic and recurses into itself. Nothing states which one a new case
should use. This is accidental complexity and would shrink to one entry point with an explicit
resolution strategy.

**Second: `HierarchyAnalyzer` has grown five jobs** (project selection, the hierarchy walk, symbol-key
bookkeeping, generated-code marking, the interface index) in 766 lines. The walk itself is small; the
surroundings accumulated.

Neither is a structural problem. The two-phase split and "only the builder writes" hold, and fitting
primary constructors in afterwards required bending neither — which is the best available evidence that
the foundation is sound.







TODO Remove

## Wo der neue Code eingehängt ist

Vier Einhängepunkte, alle in bereits existierenden Dispatch-Stellen — kein neuer Pfad:

| #    | Stelle                                                       | Auslöser                            | erzeugt                                        |
| ---- | ------------------------------------------------------------ | ----------------------------------- | ---------------------------------------------- |
| 1    | `ProcessNodeForHierarchy` → `case ParameterSyntax`           | jeder Parameter im Syntaxbaum       | Property (nur wenn ein Record eine deklariert) |
| 2    | `ProcessNodeForHierarchy` → nach `GetOrCreateCodeElementWithNamespaceHierarchy` | `symbol is INamedTypeSymbol`        | `.ctor`-Element                                |
| 3    | ↳ `CreateCapturedParameterElement`, aus 2 heraus             | je Parameter des Primärkonstruktors | Field (nur wenn eingefangen)                   |
| 4    | `AnalyzeMethodRelationships` → `if (syntax is TypeDeclarationSyntax)` | Phase 2, pro Methodenelement        | statt Body-Walk: nur die Base-Argumentliste    |

**1 und 2 laufen für jeden Typ bzw. jeden Parameter der Solution** — sie sind heiße Pfade. Deshalb steigen beide zuerst billig aus: 1 prüft `parameterSyntax.Parent?.Parent is not TypeDeclarationSyntax` (schließt jeden Methoden- und Lambda-Parameter sofort aus), 2 prüft `node is not TypeDeclarationSyntax { ParameterList: { } }`.

## Wird etwas doppelt durchlaufen?

Die gefährlichste Stelle ist Punkt 4, und dafür gibt es eine harte Messung. Für

```csharp
class Derived(int n) : Base(Helper.Scale(n))
{
    private readonly Money _m = new Money();
    public int Twice() { return n * 2; }
}
```

hat der Konstruktor genau diese Kanten:

```
Derived..ctor --Calls--> Base..ctor
Derived..ctor --Calls--> Helper.Scale
Derived..ctor --Uses---> Derived.n
```

**Nichts aus `_m` und nichts aus `Twice`.** Hätte der Body-Walk die Typdeklaration bekommen, stünde hier auch `Creates Money` und alles aus `Twice`. Das ist der Beleg, dass die Einschränkung greift.

Ein Hinweis zur Methodik: Ich habe zwischendurch auf `locs=1` als Indiz geschaut — **das trägt nicht.** `AddRelationship` dedupliziert Locations über `Except`, ein doppelter Walk derselben Stelle wäre also unsichtbar. Die Abwesenheit der fremden Kanten ist der belastbare Beleg, nicht die Location-Zahl.

Die übrigen Doppel-Kandidaten, durchgeprüft:

| Kandidat                                                     | Ergebnis                                                     |
| ------------------------------------------------------------ | ------------------------------------------------------------ |
| Base-Argumentliste zweimal (Typ- *und* Ctor-Pfad)            | Nein — der Aufruf aus dem Typpfad ist mit Schritt C gelöscht; `Derived` selbst hat nur noch `Inherits Base` |
| Field-Initializer im Ctor-Walk                               | Nein — der Walk sieht nur die Base-Liste; `_m --Creates--> Money` hängt am Feld |
| Record-Parameter wird Property **und** Capture-Field         | Nein — `AssociatedSymbol != null` beim Property-Backing schließt es aus |
| Partial Type, `CreatePrimaryConstructorElement` je Deklaration | Nur eine trägt die Parameterliste; zusätzlich dedupliziert `GetOrCreateCodeElement` über den Symbolschlüssel, `CreateCapturedParameterElement` über `ContainsKey` |
| Ordinärer Parameter löst den neuen Phase-2-Zweig aus         | Nein — der Zweig verlangt ein existierendes Element, und nur eingefangene haben eins |

Ein Fall, den ich extra gemessen habe, weil er unklar war: ein Record, dessen Parameter im Methodenrumpf gelesen wird.

```csharp
record Reader(Money Total) { public decimal Get() { return Total.V; } }
   →  Reader.Get --Calls--> Reader.Total
```

`Total` bindet an die **Property**, nicht an den Parameter. Es entsteht kein Capture-Field, also auch keine zwei konkurrierenden Knoten für dasselbe.

## Was tatsächlich neu redundant ist

Eine Sache — und die solltest du kennen, weil sie in der Graph-Ansicht sichtbar wird. Bei

```csharp
record Order(int Id, Money Total);
```

steht die Abhängigkeit auf `Money` jetzt **zweimal** im Graphen:

```
Order..ctor --Uses--> Money      (Parametertyp, über den normalen Methodenpfad)
Order.Total --Uses--> Money      (Property-Typ, über den normalen Property-Pfad)
```

Vorher war es eine Kante am Typ. Dasselbe bei `Service`: `Service..ctor --Uses--> ILogger` neben `Service.logger --Uses--> ILogger`.

Das ist kein Doppel-Durchlauf, sondern zwei verschiedene Quellelemente, die beide wirklich von `Money` abhängen — und es ist exakt das, was die ausgeschriebene Langform schon immer erzeugt hat. Auf Typ-Ebene fällt es beim Anheben ohnehin zu einer Kante zusammen, Zyklen und Schichten sehen also unverändert dasselbe. In der Detailansicht siehst du künftig zwei Kanten, wo eine war.

## Der zweite sichtbare Effekt

`class Service(ILogger logger)` hat im Baum jetzt ein Feld `logger`, das im Quelltext nirgends als Feld steht. Das ist gewollt — es ist echter Speicher, den der Compiler anlegt — aber es ist eine Zeile im Baum, die ein Leser nicht im Code wiederfindet. Falls das stört, wäre die Stelle `CreateCapturedParameterElement`; ein anderer Name (`logger` → etwas Markiertes) wäre eine reine Anzeigefrage und würde am Graphen nichts ändern.
