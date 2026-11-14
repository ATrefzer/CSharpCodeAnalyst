# Corrections and updates

## Maintaining this log

New or substantially revised entries should explain the decision, not just list the changed methods.
Use the PR #116 entries below as examples; older chapters need not be reformatted without a reason.

Include:

1. **Provenance:** link the commit that introduced the behavior and, when useful, the PR. Use the
   implementation commit rather than a later documentation-only commit. Verify the hash; do not guess it.
2. **A small C# example:** show the trigger and enough context to understand which symbols are involved.
   Mark excerpts that depend on surrounding declarations.
3. **The relevant Roslyn shape:** a reduced syntax tree with actual node kinds and named child roles.
   For a symbol-resolution issue, also show the bound symbol and declaration it resolves to. For a
   pipeline-ordering issue, supplement the syntax with the processing order instead of inventing a
   syntax distinction.
4. **Before and after:** describe the previous graph error, the intended owner/target/relationship type,
   and why this is the right level of detail.
5. **Boundaries:** distinguish graph conventions from runtime claims and explicitly identify cases
   that the change does not cover.
6. **Evidence:** name/link the relevant regression fixtures and describe what their assertions establish.

Validate syntax diagrams with the repository's Roslyn version, not by analogy with similar source
text. The PR #116 diagrams in this document were checked with Roslyn 5.9.0. They are reduced trees:
tokens, trivia, argument lists or unrelated declaration children may be omitted, but retained
parent/child paths must remain accurate. Labels such as `Left` and `WhenNotNull` name syntax
properties; displayed node kinds are not new CodeGraph node kinds.

Only `GetSymbolInfo` is insufficient to answer some contextual questions; avoid claiming Roslyn
cannot answer them at all when its operations model or other semantic APIs can provide the context.
Keep performance concerns separate from measured results and pending proposals separate from
implemented behavior.

## Field and property initializers

Constructors may be called as part of a field or property initialization:

```
public class FieldInitializers
{
    private BaseClass _baseClass = new BaseClass();

    private static List<BaseClass> _baseClassList = [new()];

    public BaseClass Prop { get; } = new();
}
```

An object creation in an initializer is anchored on the **initialized member itself**: the field or
property *creates* the constructed type and *calls* its explicit internal constructor - the same
anchoring every method invocation inside an initializer always had (`static readonly X = Create();`
gives the field a Calls edge to `Create`).

It used to be special-cased - "a field is not a calling method": the Creates edge was moved up to the
containing class, the constructor call was omitted entirely, and the field got a consolation Uses edge
to the created type. Two things killed that modelling. The omitted constructor call surfaced as a
dead-code false positive: a constructor only called from field initializers
(`static readonly List<QuickInfo> Default = [new("...")];`) had no incoming reference anywhere and was
reported with high confidence. And the class-level Creates was one level too coarse for member-level
analyses (the cohesion partitioning groups members by shared dependencies - the field is what owns
this one).

"A field calls a constructor" reads odd only under a runtime interpretation. The graph models which
element *owns* a dependency, not stack frames - the true runtime caller (the instance constructors, or
the implicit `.cctor`) often has no element at all, which is exactly why the old modelling had to
invent a stand-in anchor.



## Implicit object creation

```
new()
new Class(); 
```

**new()** is ImplicitObjectCreationExpressionSyntax, **new Class()** is ObjectCreationExpressionSyntax.

But both expressions derive from BaseObjectCreationExpressionSyntax

They are different cases in the MethodBodyWalker, but I can handle them in the equally.

## Lambdas

Lambdas inside a method are treated specially. The method that creates the lambda gets a "uses" relationship with all types in the lambda. It needs to know these types to make the lambda.
However, method calls in the lambda are not considered. This is because I don't know when the lambda is actually invoked.
That would mean to analyze the code flow.

(Method and constructor *references* inside a lambda do get `Uses` edges - see *Object creation inside lambdas* below. Only a `Calls` edge is never asserted.)

**Nested lambdas** (`() => () => Compute()`) are walked with the same `Uses` semantics: the inner body is "deferred twice", which is still deferred, so nothing changes for the modelling. They used to be skipped entirely, which silently lost every dependency inside the inner lambda.

**Invocations in lambdas** run through the same handler as method bodies (`AnalyzeInvocation`, with `Uses` instead of `Calls`); the lambda walker no longer has its own copy of the logic. Two deliberate differences remain: no call-style attributes (only the extension-method marker) and no event-invoke detection - referencing an event in a lambda does not assert that it fires. Side effect of the unification: the lambda path now reduces extension methods too, so `x => list.MyExtension()` targets the extension *method* instead of falling back to its containing static class (the old copy lacked the `ReducedFrom` handling, a symbol-key mismatch).

## Constructors of generic types not detected

The problem was that constructors are never generic in C#. 

So **ISymbol.IsGeneric** returns always false. But for Generics we need the **OriginalDefinition** found in Phase 1.

TestCase in TestSuit: GenericUtilities.GenericPair in TestSuite.

## Property accessors (get/set split)

A property is a single symbol to Roslyn, but merging its getter and setter onto one node in the dependency graph creates **phantom cycles**: if type A only *reads* `B.Value` while type B only *writes* `A.Other`, the merged property nodes carry edges that never occur together. Cycle detection then reports a cycle that does not actually exist, because in reality the getter and the setter are independent.

To fix this each property is split into separate `get_Prop` / `set_Prop` accessor nodes. This used to be gated behind a `SplitPropertyAccessors` parser option, on by default; the option was later removed and splitting made unconditional - there was no scenario where turning it off was the right call, so the toggle was deleted rather than left sitting at its default.

### The Roslyn side: the symbol does not tell you get vs set

When code accesses a property — `obj.Prop` or `Prop = x` — `GetSymbolInfo` returns the **`IPropertySymbol`**, never the accessor method. The information "this is a getter access" or "this is a setter access" is not part of the bound symbol; on the semantic level a property access is a single unit. The accessor methods only exist as `propertySymbol.GetMethod` / `SetMethod` (each an `IMethodSymbol` named `get_Prop` / `set_Prop`), and the split into two methods happens later, at IL generation.

So we have to combine semantic and syntactic information:

```
obj.Prop = x
   │
   ├─ GetSymbolInfo  → IPropertySymbol            (which property?)
   └─ Classify(node) → Write                       (get or set?)
        │
        └─ Lookup propertySymbol.SetMethod.Key()  → node "set_Prop"
```

### Classifying read vs write is purely syntactic - and small

`PropertyAccessClassifier` decides get / set / both from the syntax position alone, no semantic model needed. The key simplification: **C# does not allow passing a property by `ref` / `out`** (CS0206). That leaves only three write contexts; everything else is a read:

- assignment target `Prop = x` → setter
- compound assignment / increment `Prop += 1`, `Prop++`, `--Prop` → getter **and** setter (read-modify-write)
- everything else (`x = Prop`, `M(Prop)`, `Prop.Field`, ...) → getter

Because a property can never be a `ref`/`out` argument, "is the node the left side of an assignment or the operand of `++`/`--`?" is all we need to look at.

### How it is wired into the two phases

- **Phase 1** creates the `get_` / `set_` child nodes from `GetMethod` / `SetMethod` (symbol-based, so auto-properties, indexers and synthesized record accessors are covered too) and maps each accessor's `Key()` so phase 2 can resolve it. The accessor symbols are deliberately **not** added to the phase-2 work list (`ElementIdToSymbolMap`): the property container drives body analysis and routes each accessor body to its node. Adding the accessor symbol there as well would make phase 2 walk the bodies a second time.
- **Phase 2** routes outgoing edges (the accessor body) to the matching accessor node, and incoming property accesses to `get_` / `set_` based on the classifier. If no internal accessor node exists (external property), it falls back to a relationship to the property / containing type as before.
- **Implements / Overrides** are modeled at the accessor level (a getter implements/overrides a getter, a setter a setter), mirroring how methods work. This keeps the "follow incoming calls" abstraction walk and the cycle classifier treating accessors exactly like methods.

Now a pure read and a pure write of the same property end up on different nodes, and the phantom cycle disappears. The algorithms (Tarjan SCC, the explorer traversal) stay completely transparent to this - they only ever see more nodes and edges.



## `nameof(...)` references are compile-time, not accesses

`nameof(Prop)` looks like it touches the property, but it does not: nameof is a compile-time construct that yields a string. No getter is invoked, nothing is read or written. Yet there **is** a real dependency - it is enforced by the compiler: rename or remove `Prop` without updating the `nameof`, and the code no longer compiles.

So the reference should be modelled, but as a plain `Uses` edge to the **property symbol itself** (the container node), not as a `Calls` to the getter. This is consistent with how fields and methods inside nameof were already handled (`nameof(_field)` → `Uses` field).

Detecting it is purely structural: the path from the referenced name up to the enclosing nameof can only run through member access (for qualified names), the argument and the argument list. `SyntaxExtensions.IsInsideNameOf` walks exactly those, then checks for an `InvocationExpressionSyntax` whose expression is the identifier `nameof` **and** that binds to no symbol - the null-symbol check rules out the pathological case of a real method literally named `nameof`.

Without this the property would be classified as a read and routed to `get_Prop` - a getter call that never happens. (The split only made the issue visible; before it, the same reference was a spurious `Calls` to the property container.)



## Object creation inside lambdas

A lambda body is recorded with `Uses` edges, not `Calls`/`Creates`, because we don't know when (or whether) the lambda runs - see *Lambdas* above. For method calls inside a lambda this already produced a `Uses` edge to the **method**. Object creation, however, only recorded a `Uses` to the **type**, never to the constructor:

```csharp
imbalances.Select(i => new EventImbalanceViewModel(i));
```

That left the constructor looking unused - nobody referenced it - even though it is clearly referenced in source.

The fix records the constructor too, as a `Uses` edge (mirroring the method-call case). The model is now symmetric:

|             | type edge     | member edge          |
|-------------|---------------|----------------------|
| normal body | `Creates` → T | `Calls` → `T..ctor`  |
| lambda body | `Uses` → T    | `Uses` → `T..ctor`   |

Both relationships are downgraded from "hard" to "soft" inside a lambda. We deliberately do **not** emit a `Calls` between the constructors (some tools, e.g. NDepend, do): the outer constructor only *builds* the lambda; that `Select` later invokes it is library knowledge the parser does not have. A `Calls` would assert a control-flow edge that does not exist. `Uses` is the honest relationship - a real compile-time dependency (rename/remove the constructor and the lambda no longer compiles) without claiming a run-time call.

Same guard as the normal path (see `AnalyzeObjectCreation`): a constructor gets the edge when it is an element of the graph. Implicit and external constructors are not, and are covered by the type `Uses`.



## Indexer access (element access expressions)

`store[key]` invokes an indexer - a property spelled with brackets. The declaration side was always modelled (phase 1 creates a `this[]` property element, overload-aware via the parameter list in the symbol key, and phase 2 walks its accessor bodies), but the **usage** side was not: no walker visited `ElementAccessExpressionSyntax`, so no caller ever got an edge to the indexer. Internal indexers always looked unused.

The syntax is tricky in two ways:

1. The conditional form `store?[key]` is **not** an `ElementAccessExpressionSyntax`. Like `obj?.Member` (member binding), the `[key]` part is a separate node type, `ElementBindingExpressionSyntax`, sitting under the `ConditionalAccessExpressionSyntax`. Both node types resolve to the indexer's `IPropertySymbol` via `GetSymbolInfo` and are routed to the same handler (`AnalyzeElementAccess`).
2. Array element access (`_data[i]`) is the same syntax but resolves to **no** property symbol - arrays have no indexer in the C# semantic model. The `IsIndexer` pattern check filters it out naturally.

Since an indexer access *is* a property access, it runs through the exact same routing as identifiers and member accesses: `PropertyAccessClassifier` decides read/write/read-write (`x = store[1]` → getter, `store[2] = v` → setter, `store[3] += 1` → both; the classifier had documented element access as an expected input all along), the accessor split routes to `get_Item`/`set_Item` when enabled (Roslyn names indexer accessors after the metadata name `Item`, not `this[]`), and external indexers fall back to a `Uses` edge to the containing type. In lambda bodies the access is recorded as `Uses` instead of `Calls`, consistent with the lambda modelling above.



## User-defined operators and conversions

Applying an operator is a method call without call syntax. `a + b`, `-a`, `a == b`, `a += b`, an explicit cast `(double)celsius` and even a plain initializer `Celsius c = 21.5;` can all invoke a user-defined operator method (`op_Addition`, `op_UnaryNegation`, `op_Equality`, `op_Explicit`, `op_Implicit`, ...). The declarations were always modelled (phase 1 creates method elements for `OperatorDeclarationSyntax` / `ConversionOperatorDeclarationSyntax` and walks their bodies), but no usage ever produced an edge - user-defined operators always looked unused.

Two different Roslyn APIs are needed, because the two cases are visible in different ways:

1. **The operator is bound to a syntax node.** Binary/unary expressions, compound assignments and explicit casts bind directly: `GetSymbolInfo` on the expression returns the operator as `IMethodSymbol` with `MethodKind.UserDefinedOperator` (operators) or `MethodKind.Conversion` (casts). Built-in operators (`int +`, string concatenation, delegate `+=`) come back as `MethodKind.BuiltinOperator` and are filtered out - they are not code elements. This also naturally keeps the event `+=` handling separate: an event assignment does not bind to a user-defined operator.
2. **The conversion is invisible in the syntax.** An *implicit* user-defined conversion (`Celsius c = 21.5;`) has no node of its own; it hangs on the converted expression and is only reachable via `SemanticModel.GetConversion(expression)` (`IsUserDefined`, `MethodSymbol`). Since checking every expression would be wasteful, the walkers ask exactly at the positions where an implicit conversion can occur: initializers (`EqualsValueClause`), the right side of assignments, `return` values, arguments and expression bodies (`ArrowExpressionClause`). So the field/property initializer walks now start at the `EqualsValueClause` / arrow clause instead of the bare value expression. Not covered (accepted): conversions of operands inside larger expressions (e.g. `money + 5` converting `5`), collection-initializer entries and `foreach` element conversions.

The edge is a normal `Calls` to the operator method, `Uses` inside a lambda body (deferred execution, consistent with the lambda modelling). External operators (decimal arithmetic, `DateTime` subtraction - user-defined in metadata!) take the usual fallback to a `Uses` edge on the containing type when externals are enabled.



## Generic method groups and the Delegate-conversion quirk

Method groups (`Register(HandleString)`) have been modelled for a long time: a `Uses` edge with the `IsMethodGroup` attribute (see `IsMethodGroupReference`). Two holes remained.

**Generic method groups are a different node type.** `Create<Widget>` is a `GenericNameSyntax`, not an `IdentifierNameSyntax`, so `VisitIdentifierName` never fired for it and a standalone generic method group produced no edge at all. `AnalyzeIdentifier` now takes the common base `SimpleNameSyntax` and the walkers visit generic names too. This is safe against double handling: generic names in type positions (`List<Foo> x`) resolve to a type symbol which `AnalyzeIdentifier` ignores, as invocation target (`Create<Widget>()`) the method-group guard rejects it, and the `.Name` of a member access is owned by `AnalyzeMemberAccess` and never visited separately. The type arguments of a generic method group (`Widget` in `Create<Widget>`) are recorded as `Uses`, mirroring the generic handling of invocations - for the qualified form (`Producer.Produce<Widget>`) as well, which previously lost them.

**A method group converted to `System.Delegate` binds to no symbol.** For a `Func<...>`/`Action`-typed position (`Func<Widget> f = Create<Widget>;`) `GetSymbolInfo` returns the method. But when the target type is plain `System.Delegate` (or the conversion goes through the C# 10 natural function type), Roslyn reports `CandidateReason.OverloadResolutionFailure` with the group's members as candidates - **even though the code compiles without errors**. This affected non-generic method groups too (`Register(MakeWidget)` with a `Delegate` parameter silently produced no edge). When the symbol is null and there is exactly one method candidate, the reference is unambiguous and we use the candidate (`SingleMethodGroupCandidate`). A side benefit: in code with real overload-resolution errors (partially loaded solutions), a single-candidate reference now still yields its edge instead of nothing.

## LINQ query syntax

`from value in source where value > Threshold() select Shift(value)` is compiled into
`source.Where(value => ...).Select(value => ...)` - method calls and lambdas that never appear in the
syntax tree. No walker looked at query clauses, so two things were wrong: the implicit query-pattern
calls (`Where`, `Select`, `OrderBy`, `Join`, `Cast`, ...) were never recorded, and the clause
expressions ran through the normal method-body walker, giving `Threshold()`/`Shift(...)` a `Calls`
edge even though they only execute if and when the query is enumerated.

The modelling mirrors the compiler translation:

- **The query-pattern methods get `Calls` edges** (with `IsExtensionMethodCall` when they bind to an
  extension method). Building the query really does call `Where`/`Select` - deferred is only what
  happens *inside* the resulting sequence. Roslyn exposes the bound methods per clause:
  `GetQueryClauseInfo(clause).OperationInfo` (plus `.CastInfo` for a typed `from Foo x in ...`),
  `GetSymbolInfo(ordering)` for each `orderby` ordering, `GetSymbolInfo(body.SelectOrGroup)` for the
  final `select`/`group by` (empty for a degenerate `select x`), recursively through
  `... into g ...` continuations.
- **The clause expressions get lambda (`Uses`) semantics** - they are lambdas after translation. The
  method-body walker hands the whole query body to a `LambdaBodyWalker`; only the source of the
  *first* `from` clause keeps method-body semantics, because it is evaluated eagerly when the query
  is built. (Simplification: the inner sequence of a `join` is also evaluated at build time but
  currently gets `Uses` like the rest of the body.)
- A query nested inside a lambda is deferred as a whole: there, the operator edges are `Uses` too.
- Sub-queries nested in clause expressions are handled when the lambda walker reaches them, so their
  operators are correctly `Uses`, never `Calls`.

For the typical `IEnumerable` case the operators live in `System.Linq.Enumerable` (external →
fallback `Uses` edge to the containing type when externals are enabled); for a custom query provider
the edges point at the internal `Where`/`Select` implementations.



## Smaller implicit dependencies (batch)

A collection of smaller cases, fixed together. Common theme: the dependency is real and compiler-enforced, but either the syntax node was never visited or the declaration carrying it has no body walk.

**Attribute arguments on classes, properties, fields, events.** `[Handler(typeof(Payload))]` on a *method* was captured, because phase 2 walks the whole method declaration including its attribute lists. Types, properties, fields and events have no such declaration walk - only the `UsesAttribute` edge to the attribute class existed and the `typeof` argument was lost. `AnalyzeAttributeRelationships` now walks the attribute argument list for all non-method symbols (methods stay covered by their declaration walk, so nothing is processed twice).

A consequence worth knowing (observed on the Jellyfin reference repo, accepted deliberately): a **named attribute argument** like `[JellyfinMigrationBackup(JellyfinDb = true)]` runs through the normal property-access classification and therefore yields a `Calls` edge from the decorated element to the attribute's *property* (`JellyfinDb`), classified as a write (in fact it was classified as a read until November 2026 - see "Applying an attribute runs its constructor" below). That is technically accurate - instantiating the attribute really does set the property - even though no user code spells out the call. We keep it: special-casing attribute arguments to `Uses` would add a context check for little gain, and the edge makes attribute properties visible as used.

**Enum member initializers.** Enum members are deliberately not code elements, but that also meant `enum Level { Highest = Limits.Max }` was never walked. The initializer expressions are now walked with the dependencies anchored on the enum element itself. Note: a member referencing another member of the same enum (`All = A | B`) falls back to the containing type and yields a self-edge - consistent with recursive methods.

**Primary-constructor base-call arguments.** `class Derived() : Base(Helper.DefaultSize())` - type declarations have no body walk, so the argument expressions were lost (with a classic `: base(...)` they are part of the walked constructor declaration). They are now walked as the primary constructor's body, anchored on its own element - see the chapter on primary constructors below, which replaced the earlier type-level anchoring.

**Type arguments of constructed generics in expression position.** `Registry<Token>.Instance` - the member edge is found via normalization to `Registry<T>`, but `Token` was lost: the receiver is a `GenericNameSyntax` whose type-argument identifiers resolve to plain type symbols, which the identifier analysis ignores. A constructed generic type named in expression position now records `Uses` edges for its type arguments. In type positions (declarations, casts, creations) the same edges are already produced by the declaration handlers and simply merge.

**stackalloc.** `stackalloc Sample[2]` in expression position (e.g. as an argument) had no handler; the element type is now recorded like an array creation (the expression type `Span<Sample>`/`Sample*` resolves down to the element type). Covers the implicit form `stackalloc[] { ... }` too.

**Compiler-invoked pattern methods.** A deconstruction (`var (x, y) = point;`, including nested patterns and the `foreach (var (x, y) in ...)` form) calls the user-defined `Deconstruct`; a `foreach` calls `GetEnumerator` (or `GetAsyncEnumerator` for `await foreach`). Neither appears as an invocation in the syntax tree; Roslyn exposes them via `GetDeconstructionInfo` and `GetForEachStatementInfo`. Both now get `Calls` edges (`Uses` in lambda bodies). Deliberately **not** recorded: `MoveNext`/`Current`/`Dispose` of the enumeration pattern - they live on the enumerator type and would be noise; the `GetEnumerator` entry point carries the dependency. Pure tuple deconstructions (`(a, b) = (b, a)`) bind no method and produce no edge. All of these route through the same helper as the query-pattern operators (`AddSynthesizedCallRelationship`): extension methods are reduced, generics normalized, externals fall back to the containing type.
## Member implementations of generic interfaces

The type-level `Implements` edge (`ItemHandler -> IHandler`) always worked, but the member-level
edges (`ItemHandler.Handle -> IHandler<T>.Handle`) were silently missing for **every** generic
interface - closed (`ItemHandler : IHandler<Item>`) and open (`GenHandler<T> : IHandler<T>`)
implementations alike. Two independent causes, both in the resolution of "who implements this
interface member":

**The interface map was keyed by the constructed interface.** `AllInterfaces` returns the
*constructed* interfaces (`IHandler<Item>`), while phase 2 looks the precomputed
interface-key -> implementing-types map up with the interface member's containing type, which is the
*definition* (`IHandler<T>`). The keys never matched for closed constructions, so no implementing
type was found at all. The map is now keyed by `OriginalDefinition.Key()`.

**Roslyn's `FindImplementationForInterfaceMember` demands the member of the constructed interface.**
Our phase-1 symbol is the member of the interface *definition*. Handing that to
`FindImplementationForInterfaceMember` only works when the interface is not generic (definition and
construction coincide); for a generic interface it returns null - even for
`GenHandler<T> : IHandler<T>`, where the interface is constructed with the class's own type
parameter. The definition member is now first mapped onto each matching construction in the
implementing type's `AllInterfaces` (matched via `OriginalDefinition.Key()`), and Roslyn is asked
with that constructed member.

Two consequences of the new resolution:

- A type implementing several constructions of the same generic interface
  (`DualHandler : IHandler<A>, IHandler<B>`) yields one `Implements` edge per construction - each
  overload of `Handle` implements `IHandler<T>.Handle`.
- The key-based matching is compilation-independent (string keys), so an interface defined in a
  different project resolves directly. The previous `FindCorrespondingSymbol` /
  `FindCompilation` cross-compilation fallback became dead code and was removed.

## Default interface methods: no self implementation

For a class that only *inherits* a default interface method (`class Greeter : IGreeter` where
`IGreeter.Greet` has a body), Roslyn's `FindImplementationForInterfaceMember` returns the interface
member itself as the implementation. That used to become an `Implements` **self edge**
(`IGreeter.Greet -> IGreeter.Greet`). Such an implementation is now skipped: the inheriting class
adds nothing of its own, so there is nothing to connect. A class that *overrides* the default
implementation gets its normal member `Implements` edge. The body of the default implementation is
walked like any method body (the interface method is a regular code element).

## Partial methods and properties: two symbols, one element

The definition part (`public partial void Hook();`) and the implementation part
(`public partial void Hook() { ... }`) of a partial method are **two different `IMethodSymbol`s**
with the same symbol key. Phase 1 therefore creates one element and stores whichever symbol it saw
first (this is also what the "Found element with multiple symbols" trace warning fires on). Phase 2
walked only the stored symbol's `DeclaringSyntaxReferences` - and the definition part has no body,
so with the definition first (declaration order in the source!) all dependencies of the
implementation body were silently lost. Systematic for source generators, where the user writes the
definition part and the generator supplies the body.

Phase 2 now walks the declarations of **both** parts
(`GetDeclaringSyntaxReferencesIncludingPartial`, using `PartialImplementationPart` /
`PartialDefinitionPart`), for methods and for partial properties (C# 13) alike. The source metrics
measure the implementation part. Partial *events* (C# 14) are not special-cased yet.

## XAML: the half the markup compiler does not generate

The WPF markup compiler writes a partial class per XAML file (`obj/.../MyView.g.cs`) and MSBuildWorkspace
runs that pass during its design-time build, so the file is part of the compilation even for a solution
that was never built. It contains the event handler wiring (`IComponentConnector.Connect`, and
`IStyleConnector.Connect` for handlers inside templates) and one field per `x:Name`. Those references are
therefore plain C# and need nothing special.

Two things are *not* in there, and both were mistaken for dead code before:

- Everything declarative - element tags, `{x:Static}`, `{x:Type}`, `{Binding}`, `{StaticResource}` - is
  compiled into BAML and resolved by reflection at runtime.
- `x:Name` only produces a field in the file's **main name scope**. A `DataTemplate`, `ControlTemplate` or
  `Style` is its own name scope and gets no field. `MainWindow.xaml` in this repository has ten `x:Name`s
  and nine generated fields; the missing one sits inside a `DataTemplate`.

So a control can be used three times in XAML, once even with a name, and produce no C# reference at all.

A third pass (`Xaml/XamlReferenceExtractor` + `Xaml/XamlGraphLinker`, enabled by
`ParserConfig.IncludeXamlReferences`) therefore reads the XAML files of each project and adds the
references that carry a **fully qualified CLR name**: element tags, `{x:Static}` and `{x:Type}`. Prefixes
are resolved through the `clr-namespace` xmlns declarations, so nothing is matched by guessing. The
relationships are `Uses` and carry `RelationshipAttribute.IsXamlReference`.

### Which files belong to a project

Roslyn cannot say: a `Project` exposes `Documents`, `AdditionalDocuments` and `AnalyzerConfigDocuments`,
and a `Page` is none of them. `Xaml/XamlFileLocator` therefore **evaluates the project file a second time**
with `Microsoft.Build.Evaluation` - the MSBuild engine `Initializer.InitializeMsBuildLocator` has already
put in place, which is why the package is referenced compile-only (`ExcludeAssets=runtime`, same trap as
in `Directory.Build.props`) and why the type must not be touched before the locator is registered.

The first version scanned the project directory instead, which reads whatever lies around: a file excluded
from the project, a leftover from another branch, or the XAML of a nested project. A file taken out with
`<Page Remove="..." />` lands in **no** item group at all - not even in the SDK's default `None` glob - so
the evaluated item list gets this right where a scan cannot. Two things to know about the item list:
the SDK contributes ~20 `PropertyPageSchema` items pointing into the dotnet installation, which is why the
item type is filtered (`ApplicationDefinition`, `Page`, `Resource`, `Content`, `None`) rather than the file
extension alone; and the default `Page` items are flagged `IsImported` because they come from the SDK
props, so that flag must *not* be used to tell ours from the SDK's.

An empty result is an answer, not a failure - a project without XAML items has no XAML, and falling back
to a scan there would bring the excluded files straight back in. The scan survives only for a project file
that cannot be evaluated at all. Cost is a few hundred milliseconds per project; one shared
`ProjectCollection` evaluates the SDK imports once.

A linked file (`<Page Include="..\Shared\Foo.xaml">`) now comes along for free, because the item carries
its real path - a side effect, not the goal. Its synthetic element (see below) is named after the file
name, because a path relative to the project directory would only produce a row of dots.

`{Binding Path=...}` is deliberately left out. Without evaluating the DataContext it is a bare member name,
and matching that across the codebase would suppress far more than it explains.

An **object element** (`<local:MyControl/>`) is not only a type reference - XAML creates the instance
there, so the constructor runs. The linker therefore also connects the type's `.ctor` elements. Without
that edge the constructor of a XAML-instantiated control has no incoming reference at all, and since the
body of such a control largely hangs below its constructor (`DynamicDataGrid` wires its search timer
there), everything it calls dies with it as soon as the dead code analysis cascades. Property element
syntax (`<local:MyControl.Items>`), attached properties and `{x:Type}` only name a type and are not
treated as an instantiation. Constructor overloads share the element name, so all of them are linked -
XAML picks the parameterless one, but the graph cannot tell them apart and a missing edge costs far more
than a superfluous one.

The source of such a relationship is the code-behind class from `x:Class`. A resource dictionary has none,
so a synthetic class named after the file path takes its place - the same device already used for top-level
statements (`GlobalStatements`). It is created only when the file actually contains a resolvable reference.
Since nothing resolves the `Source` / `StartupUri` URIs of merged dictionaries, those synthetic elements
have no incoming reference and do show up in a dead code analysis; there were six of them in this
repository.

MSBuildWorkspace note: opening a WPF project runs the markup compile through a temporary `_wpftmp.csproj`,
which can invalidate the incremental build state of the real project - a following `dotnet build` may fail
with `CS2001` for every `.g.cs` until it is rebuilt.

## Generated code is always parsed, and marked

There is no "include generated code" option any more. It used to gate `GetSourceGeneratedDocumentsAsync`,
which was doubly wrong: it covered only Roslyn *source generators* while the files on disk (`*.g.cs` from
the markup compiler, `*.Designer.cs`) always came in through `project.Documents` anyway - and switching it
off did not just hide generated members, it removed the only reference many hand-written ones have.
`IComponentConnector.Connect` is the sole caller of every XAML event handler; an `[ObservableProperty]`
is the only reader of its backing field, a `[RelayCommand]` the only caller of its method. Excluding
generated code turns all of them into dead code, so the option could produce a wrong graph and nothing
else.

`Parser/GeneratedCode.cs` marks instead: `CodeElement.IsGenerated` is set when **every** declaration of the
element sits in a generated file. "Every", because a WPF code-behind class is partial and lives in
`MainWindow.xaml.cs` and `MainWindow.g.cs` at the same time - one element with two source locations. Asking
whether *any* declaration is generated would mark the user's own class; asking about the first one would
make the answer depend on the walk order. What is left are the members that exist nowhere but the generated
half (`Connect`, the `x:Name` fields, the resource designer members), which is exactly the set a consumer
wants to leave out.

Detection follows Roslyn's own `GeneratedCodeUtilities`: the file name (`.g.cs`, `.g.i.cs`, `.designer.cs`,
`.generated.cs`, `.AssemblyAttributes.cs`, `TemporaryGeneratedFile_*`) or an `<auto-generated>` comment in
the leading trivia of the first token. Source-generated documents are marked without a check.

The flag is a marking, not a filter: nothing in the parser or the graph algorithms treats a generated
element differently. Only results do - the dead code analysis reports it with a `Generated code` note.

Both halves are covered by tests, and they need different machinery. The files on disk are reachable from
an in-memory parse, so `GeneratedCodeTests` passes a document path (`Widget.g.cs`) or a header to
`ParseSourceAsync`. The source generators are not: `GetSourceGeneratedDocumentsAsync` needs a real MSBuild
project, which is what the `TestSuiteGenerated/` fixture is for - one class using `[GeneratedRegex]`,
asserted on by `SourceGeneratorFixtureTests`. That fixture is where the partial cases are pinned: the class
*and* the partial method are completed by the generator and must stay unmarked, while everything the
generator adds beside them must not be.

## Contracts from outside the analyzed code

A member that implements or overrides something we did not analyze - `ICommand.Execute`,
`object.GetHashCode`, `CSharpSyntaxVisitor.VisitGenericName` - has **no incoming reference anywhere in the
graph**. The framework is the caller. Every such member therefore looks like dead code, and worse: it looks
like a *confident* finding, because nothing hints at doubt.

The relationship model cannot express it, in either configuration:

- With `IncludeExternals` off (the default) `AddRelationshipWithFallbackToContainingType` finds neither the
  member nor its containing type internally and adds **nothing at all**. There is no element to point at.
- With `IncludeExternals` on, only *types* become external elements ("Always returns the containing TYPE
  element"), and member relationships are flattened to `Uses`. The result,
  `VisitGenericName -Uses-> CSharpSyntaxVisitor`, is indistinguishable from a method that merely uses that
  type as a parameter. Measured on this repository, turning externals on adds 954 nodes and 78 % more edges
  and still does not answer the question.

The fact is therefore recorded **beside the graph** in `ExternalContractStore` (element id -> contract
name), carried in `ParseResult` next to the source metrics. It deliberately does not live on
`CodeElement`: that type is shared with every importer, and a field only the C# parser ever fills would sit
there empty forever. `MetricStore` established the pattern - "kept beside the code graph so the graph model
stays pure".

Two detection routes in `DeclarationAnalyzer`, both needed:

- **`override`** - the existing hook (`methodSymbol.IsOverride`, and the property equivalent) records the
  contract when the *containing type* of the overridden member is not one of ours.
- **Implicit interface implementation** - `ICommand.Execute` carries no `override` keyword, and
  `AddImplementationsForInterfaceMember` only ever walks from the *interface* side, so an external interface
  is never visited. `RecordExternalInterfaceImplementations` therefore walks the type's `AllInterfaces`,
  skips the internal ones (those get real `Implements` edges) and resolves the rest with
  `FindImplementationForInterfaceMember`. Those interfaces come from `AllInterfaces` and are already
  constructed, so the definition/construction trap documented above does not apply here.

Generic types are normalized with `OriginalDefinition` before asking whether an interface is ours -
`IHandler<Widget>` is not in the map, `IHandler<T>` is.

The store is filled from the parallel phase 2, hence a `ConcurrentDictionary`. The dead code analysis
reports such members with a note rather than dropping them, and the fact is deliberately **not** pushed to
the containing type: implementing `IDisposable` is not a use of the class, so a class whose only remaining
trace is a `Dispose` method stays reportable.

### Notifying types (INotifyPropertyChanged through an external base class)

The store carries one type-level fact beside the member contracts: `NotifyingTypes`, every analyzed type
with `INotifyPropertyChanged` anywhere in its interface set. The member-level route cannot express the
common MVVM shape `MyViewModel : ObservableObject` (CommunityToolkit.Mvvm, Prism's `BindableBase`, ...):
the implementation of `PropertyChanged` sits in the external base class, so the derived type has **no
member of its own** to record a contract on, and from the graph alone it is indistinguishable from any
other class. Only the symbol knows — `RecordIfNotifyingType` asks `AllInterfaces`, which includes the
interfaces contributed by base classes, external or not. Without this, the dead code analysis rated the
bindable properties of such view models with the highest confidence, which is exactly where a XAML
`{Binding}` proves it wrong.

The single-file parse (`ParseSourceAsync`) needed `System.ObjectModel.dll` added to its metadata
references for this: `netstandard.dll` only *forwards* `INotifyPropertyChanged`, and a forward whose
target assembly is missing leaves the type unresolved — it then silently misses from `AllInterfaces`.

### Not recorded: types handed to a serializer

A serializer reads and writes public properties by reflection, so the accessors of a DTO have no incoming
reference. Types that say so with an attribute (`[DataContract]`, `[Serializable]`) are handled in the dead
code analysis. `System.Text.Json` needs no attribute, and there the only evidence is the call:
`JsonSerializer.Serialize(log)`.

Recognizing that call in the parser was built and taken out again (November 2026). It worked - but it
meant a list of serializer type names, three hooks in the phase 2 analyzers and a walk over property
types, all of it for one analysis. That is knowledge about libraries, not about the code, and it does not
belong in the component that builds the code graph. The line this draws: the parser records what the
code *is* (an override of an external member, an interface in the type's interface set); it does not
learn what a particular library *does* with it.

The consequence is accepted: an attribute-less DTO shows up in the dead code analysis with its unread
getters or uncalled setters.

## The primary constructor initializes the positional properties

```csharp
public record Order(int Id, Money Total);
```

`Id` is a property with a `get` and an `init` accessor, and both are elements. The constructor stores the
argument - but the compiler writes the backing field directly, so nothing ever *calls* `set_Id`. The init
accessor is only called by a `with` expression or an object initializer, and most records see neither.

The dead code analysis reported one accessor per positional parameter for it: 87 findings on this
repository, 60 of them with the highest confidence. Every one is true to the letter and none can be acted
on - the accessor is not written anywhere, so there is nothing to delete.

The primary constructor now gets a `Calls` edge to the init accessor of each property its parameter list
declares (`LinkPositionalPropertyInitialization`). That is not what the IL does, but it is what happens:
the constructor sets the property. The graph models which element owns a dependency, and the backing field
is not an element - the same reasoning that anchors an initializer on its field rather than on the
invisible constructor that really runs it.

What is deliberately left alone:

- **The getter.** "Nobody reads `Order.Total`" is a real statement about the record - an unused parameter -
  and stays a finding.
- **A written-out property.** In `record Explicit(int Id) { public int Id { get; init; } = Id; }` the
  property's declaration is the property, not the parameter, so it is not found here; its initializer is
  anchored on the property itself. That `init` is the developer's own and can be removed.
- **Class and struct primary constructors.** Their parameters declare no members at all.

For every other analysis the edge is invisible: it stays inside the type, so cycles, layers and the type
graph do not see it, and the constructor is dropped from the cohesion partitioning as a lifecycle member.

## The base constructor call nobody writes

```csharp
public abstract class Base
{
    protected Base() { _log = CreateLog(); }
}

public class Derived : Base
{
    public Derived() { }          // runs Base() - no ": base()" needed
}
```

A written `: base(...)` is a `ConstructorInitializerSyntax` and has always produced a `Calls` edge. Without
it there is no syntax node to visit - so `Base()` had no incoming reference. That is the normal situation
for a protected constructor, and the dead code analysis reported it.

The call is now linked in all three shapes it takes:

| The derived class has | Source of the `Calls` edge | Target comes from |
|---|---|---|
| a constructor without initializer | that constructor | Roslyn |
| a primary constructor, base clause without argument list | the primary constructor | our own ranking |
| no constructor at all | the **type** | our own ranking |

The third row is the modelling decision. The compiler generates a constructor there, and that one makes the
call - but a generated constructor is not an element. The type carries the edge instead, the same anchoring
an enum member initializer gets: the graph models which element owns a dependency.

**Which base constructor is meant** has two answers, because Roslyn only gives one for the first row.

For a written constructor the compiler has bound the call although nobody wrote it:
`SemanticModel.GetOperation(constructorDeclaration)` returns an `IConstructorBodyOperation` whose
`Initializer` is an implicit invocation of the base constructor (`LinkImplicitBaseCallOfWrittenConstructor`).
That is the compiler's own overload resolution and therefore exact. The first version of this chapter
claimed Roslyn had no symbol for an unwritten call - true for the syntax-based `GetSymbolInfo`, wrong for
the operation tree.

For the other two rows there is nothing to ask: `GetOperation` returns null for the type declaration, its
parameter list and its base type, and a generated constructor has no syntax at all. There the candidates
are ranked by hand (`FindBaseConstructorsCalledWithoutArguments`), in the order C# ranks them for an empty
argument list:

1. the parameterless constructor,
2. a constructor whose parameters are all optional,
3. a constructor that needs its `params` array to be callable without arguments.

The order matters: with `Base(params int[] values)` declared before `Base(int value = 0)`, C# calls the
second, and a "first applicable" rule linked the first - leaving the constructor that really runs without
an edge. Within tier 3 C# goes on to compare parameter counts and element types (`params string[]` beats
`params object[]`). That is not rebuilt; when several constructors are left in a tier, **all** are linked.
An edge too many hides a finding, an edge on the wrong constructor invents one.

Only constructors the derived type may call are candidates, and they are filtered **before** the ranking:
an internal `Base(int value = 0)` in another assembly is the better match on paper and cannot be called at
all, so the protected `Base(params int[] values)` next to it is the one that runs. Accessibility is asked
of Roslyn (`Compilation.IsSymbolAccessibleWithin`) rather than rebuilt - the first version only excluded
`private` and got exactly that case wrong. A private constructor still counts for a type nested in the
base type (the closed hierarchy `abstract class Shape { private Shape() { } class Circle : Shape; }`), an
internal one inside its assembly or for a friend assembly.

What gets no edge:

- **A constructor chaining to `this(...)`** - it reaches the base through the other constructor.
- **A static constructor** - it runs no base constructor.
- **A generated or external base constructor** - neither is an element. Same guard as the written call;
  the `Inherits` edge has to do.

The edge carries `IsBaseCall` like its written twin, so following incoming calls treats both alike. It adds
no dependency between types that the `Inherits` edge did not already state.

## Applying an attribute runs its constructor

```csharp
[Mark(1, Name = "x")]
public class Decorated;
```

Two things happen here that the syntax does not spell as calls.

**The constructor.** `[Mark(1)]` names the attribute *type*; the only edge was `UsesAttribute` to the class,
and `MarkAttribute(int)` had no incoming reference. Roslyn has resolved the overload already
(`AttributeData.AttributeConstructor`), so `AnalyzeAttributeRelationships` adds a `Calls` edge from the
decorated element to it - for every element kind, in the one place that also writes `UsesAttribute`. As
with object creation there is no fallback to the type: an attribute without a written constructor gets no
edge.

**The named argument.** `Name = "x"` sets the property, but it is a `NameEqualsSyntax`, not an assignment
expression. The property access classifier only knew assignments and increments as writes, so this fell
through to "read": the decorated element called `get_Name`, and `set_Name` - the accessor that really
runs - was reported as dead. The classifier has a third case now. It is restricted to a `NameEquals` whose
parent is an attribute argument, because the same node appears in `new { Name = x }`, where no setter
exists.

Not covered: attributes on **parameters, return values and type parameters**. Those symbols are not
elements, so `AnalyzeAttributeRelationships` never sees them - not for the constructor, and not for
`UsesAttribute` either.

## Visibility on the code element

`CodeElement.AccessLevel` carries what `ISymbol.DeclaredAccessibility` says, mapped in `HierarchyAnalyzer`
where the elements are created (types and members in one place, property accessors in another - an accessor
may narrow its property, `public int P { get; private set; }`).

Unlike the external contracts, this belongs **on** the element rather than beside it: visibility is a
first-class property that every language the tool imports has, several consumers can use it, and the
importers can fill it later. `AccessLevel.Unknown` is the default and must always be read as "nobody told
us", never as a value - a graph from doxygen or jdeps has no visibility today, and neither has a project
file written before this existed.

The type is called `AccessLevel`, not `Accessibility`: WPF drags a global `Accessibility` namespace into
scope, so the natural name would force a full qualification in every file of the UI projects.

Persisted in both formats - `SerializableCodeElement` (optional constructor parameter, so old project files
keep loading) and the text serializer (`access=` written only when it is not Unknown, and an unparsable
value falls back to Unknown rather than guessing).

The dead code analysis uses it for the confidence of a finding, and reads it over the whole containment
chain: a `public` method of an `internal` class is just as unreachable from another assembly, so it is the
*most restrictive* container that decides.

Two members were found only through this: a **static constructor** and a **finalizer**. Both are run by
the runtime, can never be referenced from code, and are effectively private, so they landed in the highest
confidence band. (They were recognized by their names at the time; today that is
`MemberRole` - see below.) The static constructor started out annotated as an entry point; both are now
dropped from the result entirely - on a live type such a row is wrong in every case, and on a dead type
the roll-up covers them.

## Type parameters are part of the element name

`WpfCommand` and `WpfCommand<T>` are two declarations and two code elements - `SymbolExtensions.Key`
has always told them apart. Their *names* did not: both `Name` and `FullName` came from `symbol.Name`,
which carries no type parameters, so the two were indistinguishable in the tree, the graph labels, every
export and every architectural rule. `SymbolExtensions.GetDisplayName` now appends the type parameter
list a generic type, interface or method declares - `Cache<T>`, `Map<TKey,TValue>`, `Foo<T,T2,T3>` - and
both `Name` and `FullName` (via `BuildSymbolName`) are built from it. `CodeElement.GetFullPath` joins the
`Name`s and therefore follows along on its own; the two ways of spelling a full path stay in agreement.

Three decisions in that one line are worth keeping:

- **The list comes from the type *parameters*, never from the arguments of a construction.** So
  `List<int>` and `List<T>` both yield `List<T>`, which makes the name independent of which symbol
  instance phase 2 happens to hold, and matches the rest of the parser: every arrow already points at the
  `OriginalDefinition`. It also keeps the name free of dots and spaces, which the outputs depend on - the
  plain text format splits its lines on whitespace, the DSI hierarchy splits names on dots.
  A visible consequence: `CreatePair<T,U>` calling the constructor of `GenericPair<TFirst,TSecond>` shows
  the *declaration's* parameter names at the target, not the ones the call site passes.
- **`Arity`/`TypeParameters`, not `IsGenericType`.** `INamedTypeSymbol.IsGenericType` is documented as
  "true if this type **or some containing type** has type parameters", so `List<T>.Enumerator` reports
  true while having no parameters of its own - it would have been named `Enumerator<>`.
  `IMethodSymbol.IsGenericMethod` does not have that property, but asking for the parameter list is the
  more direct question either way. Note that `GetGenericPart`, which builds the *key*, does use
  `IsGenericType` and does produce `<>` for such types; harmless there, because both sides of a key
  comparison do it, but not a pattern to copy.
- **Overloads still share a name.** Two methods that differ only in their parameter lists
  (`M2<T>(int, T)` and `M2<T>(T, int)`) remain one name, as they always were - the parameters are part of
  the `Key`, not of the name. Only arity overloads (`M1<T>` vs `M1<T,T2>`) became distinguishable.

One consumer had to be taught about it, and one turned out to be *fixed* by it:

- `XamlGraphLinker.BuildTypeLookup` keys types by their CLR name to resolve what markup refers to, and
  needed no change at all - the type parameter list stays in the key, and that is what finally makes the
  key unique. While `Cache` and `Cache<T>` were both named `Cache` they collided on one key and
  `types[key] = element` let whichever came later in the node order silently win, so a markup reference
  to `Cache` could resolve to the generic type. Now the keys differ and `Cache` resolves to exactly the
  non-generic declaration. Stripping the list back off inside the lookup - the seemingly obvious move -
  restores the collision; `Link_GenericAndNonGenericTypeOfTheSameName_ResolvesToTheNonGenericOne` guards
  against it. An open generic is unreachable from markup through this route anyway, because
  `XamlReferenceExtractor` does not read `x:TypeArguments`.
- The architectural rules match a path against `FullName` exactly, which would have made every existing
  rule naming a generic element silently dead. `RuleParser` now accepts a type parameter list in a path,
  and `PatternMatcher` resolves a path *without* one to the generic element as well: `MyApp.Cache`
  matches `Cache` and `Cache<T>`, `MyApp.Cache.Add` also `MyApp.Cache<T>.Add`. Writing the list out
  selects exactly one of them. Where a short path names both, the rule means both - the same reading that
  has always applied to overloaded members.

## What a member is for: `MemberRole`

Two analyses need to know whether a method exists to do work or to bring an object into (or out of) a
valid state. The type cohesion metric drops the lifecycle members before partitioning - a constructor
assigns most of the state, so in the member graph it is a clique over all fields and would merge every
group into one, which is exactly what makes the partition count an LCOM4 reading rather than a
reachability check. The dead code analysis drops the static constructor and the finalizer, because
nothing can reference them and "nothing references it" therefore carries no information.

Both used to ask by **name**: `.ctor`, `.cctor`, `Finalize`. Those are the CLR metadata names, which
Roslyn reports as `ISymbol.Name` - so the test worked, but only ever for the C# parser, and the two
analyses had written it out separately. `CodeElement.MemberRole` replaces it with a statement from
whoever produced the element, filled in `SymbolExtensions.GetMemberRole` from `IMethodSymbol.MethodKind`.

The reason it is not simply a wider name list: **no name test can hold for more than one language.** A
C++ constructor is called like its class and its destructor `~Foo`; Python uses `__init__` and `__del__`;
and a Dart named constructor `Foo.fromJson` arrives as a method called `fromJson`, which no rule could
ever tell apart from an ordinary method of that name. The producer knows; the graph did not carry the
answer.

The enum is deliberately four-valued plus `Unknown`, and the pair `Unknown` / `Normal` is the load-bearing
part:

- `Unknown` means **nobody told us** - a producer that does not fill roles, or an element that is not a
  method at all. As with `AccessLevel`, it must never be read as a value.
- `Normal` means the producer **looked and says this is ordinary**. A producer that fills roles at all
  says this about every method it creates, which is what keeps `Unknown` reserved for "not filled in".

There is deliberately **no name-based fallback**. A graph whose producer does not fill roles simply has
no lifecycle members - the cohesion numbers of such a graph are a reachability check rather than an LCOM4
reading, and the dead code analysis reports its static constructors. That is the visible consequence of
loading a **project file saved before this existed**: it keeps loading, but it has to be imported again
to be measured correctly. Keeping the C# names alive as a shim was considered and dropped - it would have
been a second, silent definition of what a constructor is, in the one layer that is supposed to be free
of language conventions.

One thing got *fixed* on the way: a method deliberately named `Finalize` (legal C#, `public new void
Finalize()`) was read as a finalizer by the name test - excluded from the partitioning and never reported
as dead code. Roslyn calls it `MethodKind.Ordinary`, so it is now `Normal`.
`MemberRoleParseTests.AMethodMerelyNamedFinalize_IsNotAFinalizer` pins it.

Persisted in both formats, the same way `AccessLevel` is: an optional constructor parameter on
`SerializableCodeElement`, and `role=` in the text serializer, written only when it is not `Unknown` and
falling back to `Unknown` when it cannot be parsed.

(Primary constructors and a positional record's members were not in the graph when this was written -
see the next chapter, which fixed that.)

## Primary constructors, positional records and captured parameters

Phase 1 walks declaration syntax, and a primary constructor has none of its own: its declaring syntax is
the type declaration. For a long time that meant `record Order(int Id, Money Total)` and
`class Service(ILogger logger)` had **no constructor element and no members at all** - a record was an
empty type in the tree, and the dependency was kept alive by anchoring `Uses` edges for the parameter
types on the *type* element (`AnalyzePrimaryConstructorParameters`). That was a deliberate decision, and
for cycles and layers it is enough.

For the **type cohesion metric** it is not, and the measurement is the reason this changed:

```
class Captured(ILogger logger)              // C# 12: the parameter is captured
  Start, Stop  →  2 partitions              ← looks like a class that should be split

class Explicit { ILogger _logger; ... }     // the same class written out
  _logger, Start, Stop  →  1 partition      ← correctly cohesive
```

The captured parameter *is* the shared state, but it was not a node, so the two methods shared nothing.
A false split - and it gets worse the more the C# 12 idiom is used.

Three things now exist as elements:

- **The primary constructor**, found through `INamedTypeSymbol.InstanceConstructors` and the declaring
  syntax being the type declaration. Its body walk has to be restricted: handing the type declaration to
  `AnalyzeMethodBody` would attribute every member of the type to the constructor. What it actually runs
  is the argument list of the base clause, nothing else - field and property initializers are anchored on
  those members themselves.
- **A positional record's properties.** Contrary to what one expects they are *not* implicitly declared:
  they are ordinary `IPropertySymbol`s whose declaring syntax is the `ParameterSyntax`, with a normal
  symbol key. Phase 2 therefore needed no change at all - `order.Id` resolves through the existing
  property path. Roslyn offers no route from the parameter to that property, so it is found the other way
  round: the property whose declaration *is* this parameter. That also settles
  `record Order(int Id) { public int Id { get; init; } = Id; }`, where the member's declaration is the
  property and the ordinary path creates it.
- **A captured parameter**, as a `Field`. Whether a parameter is captured is the compiler's decision and
  it is readable: it emits a field named `<name>P`, implicitly declared and with no `AssociatedSymbol`. A
  parameter that is never used gets none, and one used only in a field initializer gets none either -
  there the declared field already carries the state. A record's positional parameter produces a property
  *backing* field instead, which does carry an `AssociatedSymbol` and is excluded here.

Two decisions about the captured parameter are worth stating, because the obvious alternatives are
wrong:

- It is **keyed on the parameter symbol**, not on `<name>P`. A body referring to `logger` binds to the
  parameter, so that is what phase 2 looks up. `AnalyzeIdentifier` gained a branch for it, guarded by
  "an element exists" - deliberately without the containing-type fallback, which would turn every
  ordinary parameter use in the codebase into an edge. Only the identifier path needs it: the left side
  of `logger.Log()` is visited there by the walker, and the right side of a member access can never be a
  parameter.
- It is a **child of the type**, not of the constructor. The constructor is dropped from the member graph
  as a lifecycle member (see `MemberRole`), and it would take the field with it - which is exactly the
  split this was meant to fix.

With the constructor in the graph, `IsExplicitConstructor` disappeared. It existed only to say "this
constructor has no element"; the four call sites already asked `FindInternalCodeElement(...) is not null`
next to it, which is the honest question. The visible consequence: `new Service(x)` now records
`Calls Service..ctor` alongside `Creates Service`, exactly as the long form always did. The type-level
`Uses` edges for the parameter types moved onto the constructor, where the ordinary parameter handling
produces them anyway - cycle detection and the layer rules are unaffected, because member edges are
lifted to the containing type.


## Conversion operators with the same source type

**Introduced by:** [`df0f6f9`](https://github.com/ATrefzer/CSharpCodeAnalyst/commit/df0f6f9f7149ed39109b56644aae6c3c54e303a1)
(PR #116). **Implementation:** `SymbolExtensions.GetKeyInternal`.

### Example and Roslyn shape

~~~csharp
class First { }
class Second { }
class Source
{
    public static implicit operator First(Source value) => new First();
    public static implicit operator Second(Source value) => new Second();
}
~~~

Each declaration has the same parameter type but a different target type:

~~~text
ConversionOperatorDeclaration
├─ Type: IdentifierName "First"             // "Second" in the other declaration
├─ ParameterList
│  └─ Parameter "value"
│     └─ Type: IdentifierName "Source"
└─ ArrowExpressionClause
   └─ ObjectCreationExpression "new First()"
~~~

Roslyn exposes both as `IMethodSymbol` values with `MethodKind.Conversion`. Their name
(`op_Implicit`) and parameters are equal; their `ReturnType` distinguishes them.

### Previous failure and graph decision

The ordinary method key collapsed these declarations into one element. Phase 1 kept one symbol,
and calls to either conversion could be attributed to the body of that symbol.

For conversions only, the key now includes the fully qualified return type. Each conversion keeps
its own body, type dependencies and incoming calls. Other method keys are unchanged. The graph's
display names may still both be `op_Implicit`; distinct identity does not require distinct labels.

**Regression coverage:** [ConversionOperatorIdentityParseTests](../../Tests/UnitTests/Parser/ConversionOperatorIdentityParseTests.cs)
checks implicit/explicit operators, both declaration orders, separate bodies and call targets.
This is an identity fix, not a claim that all possible conversion contexts are visited.

## External types first referenced by top-level statements

**Introduced by:** [`df0f6f9`](https://github.com/ATrefzer/CSharpCodeAnalyst/commit/df0f6f9f7149ed39109b56644aae6c3c54e303a1).
**Implementation:** `RelationshipAnalyzer.AnalyzeRelationships`.

### Example and Roslyn shape

~~~csharp
System.Console.WriteLine(1);
~~~

~~~text
CompilationUnit
└─ GlobalStatement
   └─ ExpressionStatement
      └─ InvocationExpression
         ├─ Expression: SimpleMemberAccessExpression "System.Console.WriteLine"
         └─ ArgumentList
            └─ Argument → NumericLiteralExpression "1"
~~~

There is no written method declaration to own this statement. The parser anchors it on the synthetic
`GlobalStatements.Execute` element. With externals enabled, the external method reference is
aggregated to its containing type, `Console`, through the existing `Uses` policy.

### Previous failure and graph decision

Global statements run through analysis after the parallel member loop. They can create external cache
entries for the first time. The old flush happened before that analysis, leaving an edge whose target
ID did not exist in `CodeGraph.Nodes`.

~~~text
member analysis → global-statement analysis → flush external elements → returned graph
~~~

The flush now occurs after all relationship producers have finished. It is a change of ordering,
not a second pass over external references. External aggregation and `IncludeExternals` are unchanged.

**Regression coverage:** [TopLevelExternalRelationshipParseTests](../../Tests/UnitTests/Parser/TopLevelExternalRelationshipParseTests.cs)
checks newly discovered and already cached external types, complete parent/child links and externals
disabled. The graph invariants described below also check endpoint existence.

## Compiler errors make a partial graph visible

**Introduced by:** [`df0f6f9`](https://github.com/ATrefzer/CSharpCodeAnalyst/commit/df0f6f9f7149ed39109b56644aae6c3c54e303a1).
**Implementation:** `HierarchyAnalyzer.BuildHierarchy`, `Parser.BuildAdhocSolution`.

### Example and the syntax/semantics distinction

~~~csharp
class Caller
{
    void Run() { MissingLibrary.DoWork(); }
}
~~~

~~~text
InvocationExpression
└─ Expression: SimpleMemberAccessExpression
   ├─ Expression: IdentifierName "MissingLibrary"
   └─ Name: IdentifierName "DoWork"
~~~

The syntax tree exists even though the name cannot be resolved. A successful workspace load and a
successful syntax walk therefore do not establish that all dependencies were found.

### Previous failure and graph decision

Phase 1 now adds unsuppressed compiler errors from `Compilation.GetDiagnostics()` for each selected
project to `ParserDiagnostics.Failures`. Entries include project context, diagnostic ID and source
location. Resolvable graph elements and relationships are retained: parsing remains best effort.

For snippets, a compilation unit containing global statements uses `OutputKind.ConsoleApplication`;
other snippets use `DynamicallyLinkedLibrary`. This avoids synthetic CS8805/CS5001 errors caused only
by choosing the wrong output kind. Ordinary compiler warnings are not copied to parser diagnostics,
diagnostics clear at the next public parse entry, and CLI exit-code policy is unchanged.

**Current limit:** this commit does not cap the number of compiler-error entries. Also, collecting
all compilation diagnostics entails binding work; limiting displayed entries would not eliminate
that work. Do not describe either a diagnostic cap or a measured performance improvement as part
of this change.

**Regression coverage:** [CompilationDiagnosticsParseTests](../../Tests/UnitTests/Parser/CompilationDiagnosticsParseTests.cs)
covers unresolved calls alongside resolvable edges, syntax errors, clearing and valid input.

## Normalize internal member lookup centrally

**Introduced by:** [`9c2b747`](https://github.com/ATrefzer/CSharpCodeAnalyst/commit/9c2b747e2f05bda81e2a57bada42ad35f2080ecf).
**Implementation:** `RelationshipBuilder.FindInternalCodeElement`.

### Example and Roslyn shape

~~~csharp
class Box<T> { public T Value { get; set; } }
// Inside a method whose parameter is Box<int> box:
box.Value = 1;
~~~

~~~text
SimpleAssignmentExpression
├─ Left: SimpleMemberAccessExpression
│  ├─ Expression: IdentifierName "box"
│  └─ Name: IdentifierName "Value"
└─ Right: NumericLiteralExpression "1"

bound accessor:       Box<int>.set_Value
OriginalDefinition:   Box<T>.set_Value
stored graph element: Box<T>.Value.set_Value
~~~

The syntax shape is ordinary member access. The missing step was symbol resolution: phase 1 stores
declarations, while phase 2 often sees constructed symbols.

### Previous failure and graph decision

Some callers normalized symbols themselves, while accessor, interface-implementation and event-handler
lookups could miss the declared member. A generic property access could fall back to its container;
an inherited implementation or `Handles` edge could disappear entirely.

The shared lookup now tries an exact key, an unreduced extension-method key when applicable, and the
original-definition key. Exact matches retain precedence. No constructed-instance graph nodes are added.

Named-type analysis must still collect concrete type arguments after a successful lookup. Returning
early merely because normalization found the type would lose dependencies such as `Box<Token> → Token`.

**Regression coverage:** [GenericMemberResolutionParseTests](../../Tests/UnitTests/Parser/GenericMemberResolutionParseTests.cs)
checks property/indexer accessors, inherited generic implementations, event handlers and retained type
arguments. Existing tests that expected property-container fallbacks now expect accessor edges.

## Keep arguments of qualified and nested generic types

**Introduced by:** [`4129550`](https://github.com/ATrefzer/CSharpCodeAnalyst/commit/41295507f8fc9835280cac1c010c29b86ffd17f8).
**Implementation:** `RelationshipBuilder.AddTypeArgumentRelationships`,
`SyntaxNodeAnalyzer.AnalyzeIdentifier` and `AnalyzeMemberAccess`.

### Example and Roslyn shape

~~~csharp
_ = N.Outer<Token>.Inner<Other>.Value;
~~~

The relevant expression subtree is a chain of member accesses, not a declaration-style
`QualifiedNameSyntax`:

~~~text
SimpleMemberAccessExpression
├─ Expression: SimpleMemberAccessExpression
│  ├─ Expression: SimpleMemberAccessExpression
│  │  ├─ Expression: IdentifierName "N"
│  │  └─ Name: GenericName "Outer<Token>"
│  └─ Name: GenericName "Inner<Other>"
└─ Name: IdentifierName "Value"
~~~

For the bound `Inner<Other>` type, `TypeArguments` supplies `Other`; its constructed
`ContainingType` supplies `Token`. An alias to this closed type can instead be an ordinary
`IdentifierNameSyntax`.

### Previous failure and graph decision

The member-access walkers intentionally visit the receiver but not the member name twice.
Previously a generic type in that skipped name position could lose its arguments. Processing only
the innermost type's `TypeArguments` also missed arguments of enclosing types.

The collector now follows the containing-type chain and adds `Uses` edges for concrete arguments.
Both standalone identifiers (including aliases) and member accesses that bind to named types invoke
it. Type signatures use the same collector. Existing record/CRTP self-reference suppression remains.

**Expected effect:** the member edge to `Value` is retained, and the owning element also uses
`Token` and `Other`. No separate instantiation nodes are needed.

**Regression coverage:** [QualifiedGenericDependencyParseTests](../../Tests/UnitTests/Parser/QualifiedGenericDependencyParseTests.cs)
covers namespace/global qualification, aliases, containing types, arrays and lambda bodies.

## Classify deconstruction and null-conditional assignment targets

**Introduced by:** [`49020ed`](https://github.com/ATrefzer/CSharpCodeAnalyst/commit/49020edbedebb478cf1d1d91bc9fe91556c579fc).
**Implementation:** `PropertyAccessClassifier.Classify` / `AscendAssignmentTarget`;
consumer: `SyntaxNodeAnalyzer.AddPropertyAccessRelationship`.
Named attribute argument handling predates this commit and is retained from [PR #115](https://github.com/ATrefzer/CSharpCodeAnalyst/pull/115).

### What is being classified

`GetSymbolInfo` identifies the property, not a getter/setter choice for that occurrence.
This does not mean Roslyn cannot describe the context: its operations model can represent assignment
and property-reference operations. This parser deliberately uses a small syntax classifier after
semantic property resolution.

For ordinary value-returning properties, the classifier answers `Read`, `Write` or `ReadWrite`.
It first climbs wrappers that preserve the target, then inspects the parent:

| Context after climbing | Classification |
|---|---|
| Left of a simple assignment (`=`) | `Write` |
| Left of a compound assignment (`+=`, `??=`, etc.) | `ReadWrite` |
| Operand of prefix/postfix `++` or `--` | `ReadWrite` |
| `NameEquals` under an `AttributeArgument` | `Write` |
| Other contexts | `Read` |

This is a dependency classification, not a prediction that a conditional setter will run on every
execution. For example, `??=` may only read when the value is already non-null.

### Ordinary member access: pass the entire property expression

~~~csharp
obj.Prop = 5;
~~~

~~~text
SimpleAssignmentExpression
├─ Left: SimpleMemberAccessExpression "obj.Prop"   ← classifier input
│  ├─ Expression: IdentifierName "obj"
│  └─ Name: IdentifierName "Prop"
└─ Right: NumericLiteralExpression "5"
~~~

The caller passes the whole member access. Its parent is already the assignment, so climbing stops.
`assignment.Left == node` is true and the result is `Write`.

For `x = obj.Prop`, the same test is false and the result is `Read`. For
`obj.Prop += 1`, the assignment is not simple and the result is `ReadWrite`.
A property used as the receiver in `obj.Container.Prop = 5` remains a read: climbing does not
cross a member access from its `Expression` child into the final target.

### Null-conditional assignment: the assignment is inside WhenNotNull

~~~csharp
Receiver?.P = 1;
~~~

With the repository's Roslyn 5.9.0 parser, the actual subtree is:

~~~text
ConditionalAccessExpression
├─ Expression: IdentifierName "Receiver"          ← receiver read
└─ WhenNotNull: SimpleAssignmentExpression
   ├─ Left: MemberBindingExpression ".P"           ← climb here, then stop
   │  └─ Name: IdentifierName "P"                  ← classifier input for P
   └─ Right: NumericLiteralExpression "1"
~~~

For `P`, the steps are:

1. `AnalyzeIdentifier` passes the inner `IdentifierNameSyntax`.
2. Its parent is a member binding whose `Name` is that node, so climbing moves to `.P`.
3. The next parent is the assignment. Climbing stops; `.P` is the left operand.
4. The result is `Write`, producing the setter reference.

The assignment is **not** the parent of the complete `Receiver?.P` expression in this tree.
Consequently this example does not use the classifier's conditional-access ascent branch.

For `Receiver`, the parent is a conditional access, but the node is its `Expression`, not its
`WhenNotNull`. It must not climb. If `Receiver` is a property, it contributes a getter reference.

The conditional-access ascent branch only follows a node that is exactly `WhenNotNull`. For an
ordinary read such as `var x = Receiver?.P;`, it can climb from `.P` through that conditional access
and still conclude `Read`. It never treats the receiver as a write target.

For `Receiver?.P += 1`, the guarded child is an `AddAssignmentExpression`; the same member-binding
step reaches it and yields `ReadWrite`.

### Deconstruction: tuple leaves on the left are writes

~~~csharp
(P, Q) = (Q, P);
~~~

~~~text
SimpleAssignmentExpression
├─ Left: TupleExpression
│  ├─ Argument → IdentifierName "P"                ← Write
│  └─ Argument → IdentifierName "Q"                ← Write
└─ Right: TupleExpression
   ├─ Argument → IdentifierName "Q"                ← Read
   └─ Argument → IdentifierName "P"                ← Read
~~~

An `ArgumentSyntax` is transparent only when its parent is a `TupleExpressionSyntax`.
Climbing jumps to that tuple and repeats, supporting nested targets such as `(P, (Q, R))`.
The left/right assignment check then distinguishes the two sides.

The resulting dependency set includes `get_P`, `set_P`, `get_Q` and `set_Q`.
For `Foo(P)`, the argument belongs to an `ArgumentList`, so climbing stops and `P` stays a read.

### Conditional indexer: index arguments are reads too

~~~csharp
Receiver?[P] = Q;
~~~

~~~text
ConditionalAccessExpression
├─ Expression: IdentifierName "Receiver"
└─ WhenNotNull: SimpleAssignmentExpression
   ├─ Left: ElementBindingExpression
   │  └─ BracketedArgumentList
   │     └─ Argument → IdentifierName "P"
   └─ Right: IdentifierName "Q"
~~~

The indexer analyzer passes the `ElementBindingExpression` itself, which is directly on the left
of the assignment. It is a write. In contrast, `P` is under a bracketed argument list, not a tuple;
both `P` and the right-hand `Q` are reads. The receiver remains a read as well.

This explains why the old identifier-based `?.P` path failed even though some conditional-indexer
assignment cases already worked: the two callers start from different node kinds.

### Named attribute arguments and other boundaries

~~~csharp
[Mark(Name = "x")]
~~~

~~~text
Attribute
└─ AttributeArgumentList
   └─ AttributeArgument
      ├─ NameEquals
      │  └─ IdentifierName "Name"                 ← classifier input
      └─ StringLiteralExpression "x"
~~~

There is no assignment-expression node here. The explicit `NameEquals` case selects the setter,
preserving PR #115's behavior. Parentheses are also transparent: `(Prop) = x` reaches the same
assignment as `Prop = x`. Increment/decrement expressions select both accessors.

`nameof(Prop)` is intercepted by the caller before classification. It contributes a `Uses`
reference to the property container, not a getter or setter reference.

Do not generalize the ordinary-property model to ref-return properties. A property such as
`ref int RefProp => ref storage;` can supply a writable reference, and `RefProp = 1` writes through
that reference rather than invoking a setter. Syntax alone has the same assignment shape; precise
accessor modelling for that case is outside this classifier's current coverage. Similarly, this
chapter does not claim complete modelling of every newer user-defined assignment operator.

`foreach ((P, Q) in pairs)` with existing properties is not a missing setter case: C# rejects it
with CS8186 because iteration variables must be declared.

### How the result becomes graph edges

~~~text
Read       → propertySymbol.GetMethod
Write      → propertySymbol.SetMethod
ReadWrite  → both, when available

ordinary body → Calls
lambda body   → Uses
~~~

Accessor symbols resolve through the central normalized lookup. If no requested accessor can be
linked, the existing property/containing-type/external fallback policy applies; classification does
not manufacture accessor nodes. Externals retain their aggregated `Uses` treatment.

**Previous failure:** climbing only parentheses left deconstruction leaves under `ArgumentSyntax`
and conditional property names under `MemberBindingExpressionSyntax`; both incorrectly became reads.

**Regression coverage:** [AssignmentAccessorParseTests](../../Tests/UnitTests/Parser/AssignmentAccessorParseTests.cs)
covers nested tuples, right-hand reads, receiver chains, index arguments, simple/compound/coalescing
conditional assignments and lambdas. [PropertyAccessClassifierTests](../../Tests/UnitTests/Parser/PropertyAccessClassifierTests.cs)
covers the classifier directly, including the retained named-attribute behavior.

## Field-like event initializers own their dependencies

**Introduced by:** [`4ee545b`](https://github.com/ATrefzer/CSharpCodeAnalyst/commit/4ee545b5a361ce30d6028a954c53f151bf9b082a).
**Implementation:** `DeclarationAnalyzer.AnalyzeEventRelationships`.

### Example and Roslyn shape

~~~csharp
public event System.Action Changed = Helper.Handle;
~~~

~~~text
EventFieldDeclaration
└─ VariableDeclaration
   ├─ Type: QualifiedName "System.Action"
   └─ VariableDeclarator "Changed"
      └─ EqualsValueClause
         └─ SimpleMemberAccessExpression "Helper.Handle"
~~~

The event symbol's declaring syntax identifies the variable declarator. A declaration can contain
several event variables with different initializers, so walking the entire declaration for each event
would mix their dependencies.

### Previous failure and graph decision

Event type and add/remove bodies were analyzed, but field-like initializers were skipped. The fix
walks only the matching initializer, starting at its `EqualsValueClause`, and anchors dependencies
on that event, consistently with field initializers.

| Initializer | Dependency owned by the event |
|---|---|
| `Helper.Handle` | `Uses Handle` with `IsMethodGroup` |
| `() => Helper.Handle()` | deferred `Uses Handle` |
| `Helper.Create()` | `Calls Create` |
| `new Handler().Handle` | `Creates Handler`, explicit constructor `Calls`, method-group `Uses Handle` |

No additional `Handles` or event-registration policy is introduced for initialization. Existing
assignment-based registration handling remains separate.

**Regression coverage:** [EventInitializerParseTests](../../Tests/UnitTests/Parser/EventInitializerParseTests.cs)
covers static/instance events, lambdas, anonymous methods, factories, receiver construction, multiple
declarators and empty initializers.

## Expression lambda results include deferred conversions

**Introduced by:** [`4a44792`](https://github.com/ATrefzer/CSharpCodeAnalyst/commit/4a4479263a12677813d229bc4f65d11f7c03311b).
**Implementation:** `LambdaBodyWalker.AnalyzeLambdaBody` / `AnalyzeResultConversion`,
and the simple/parenthesized lambda visitors.

### Example and Roslyn shape

~~~csharp
class Target
{
    public static implicit operator Target(int value) => new Target();
}
// Inside a method:
System.Func<Target> callback = () => 1;
~~~

~~~text
VariableDeclarator "callback"
└─ EqualsValueClause
   └─ ParenthesizedLambdaExpression
      ├─ ParameterList (empty)
      └─ Body: NumericLiteralExpression "1"
~~~

Although the source contains an arrow token, this body is not an `ArrowExpressionClauseSyntax`.
It also has no `ReturnStatementSyntax`. The literal's contextual conversion to `Target` is available
from the semantic model, not as a written cast node.

### Previous failure and graph decision

The previous conversion hooks for return statements and arrow clauses never saw this boundary.
The lambda walker now checks an expression body's conversion explicitly, then traverses the body.
Its nested-lambda visitors perform the same check while preserving normal traversal.

A user-defined conversion contributes a `Uses` edge to its operator, owned by the enclosing element.
It is not an immediate `Calls` edge. Block returns retain their existing handling; built-in numeric,
reference and identity conversions do not invent operator edges.

**Regression coverage:** [LambdaResultConversionParseTests](../../Tests/UnitTests/Parser/LambdaResultConversionParseTests.cs)
covers simple, typed, parenthesized, nested and async lambdas, block/anonymous-method controls,
explicit casts, built-in conversions and lambdas in event initializers.

## Classify static calls from the bound method

**Introduced by:** [`e4ba44c`](https://github.com/ATrefzer/CSharpCodeAnalyst/commit/e4ba44c68a112f8dbb20bf67fd9cc106629bc72d).
**Implementation:** `SyntaxNodeAnalyzer.DetermineCallAttributes`.

### Example and Roslyn shape

~~~csharp
G<int>.F();
Demo.Tools.F();
Generic<int>();
~~~

~~~text
InvocationExpression
└─ Expression: SimpleMemberAccessExpression
   ├─ Expression: GenericName "G<int>"
   └─ Name: IdentifierName "F"

InvocationExpression
└─ Expression: SimpleMemberAccessExpression
   ├─ Expression: SimpleMemberAccessExpression "Demo.Tools"
   └─ Name: IdentifierName "F"

InvocationExpression
└─ Expression: GenericName "Generic<int>"
~~~

All three can bind to static methods. Receiver syntax is not a reliable static/instance test.

### Previous failure and graph decision

The former syntax-based classification could label qualified/generic static calls as instance calls,
or leave an unqualified generic static call without a static flag. The target edge could be correct
while its dispatch metadata was wrong.

After preserving the dedicated extension-method classification, `IMethodSymbol.IsStatic` decides
static dispatch. Remaining instance syntax distinguishes `base`, explicit `this`, another receiver
and an unqualified current-instance call. Lambda invocations retain deferred `Uses` semantics rather
than acquiring ordinary call-dispatch flags.

**Regression coverage:** [CallClassificationParseTests](../../Tests/UnitTests/Parser/CallClassificationParseTests.cs)
covers short/qualified/global/alias/generic forms plus instance, this/base, extension and deferred
controls. This changes edge metadata, not the set of runtime dispatch targets.

## Explicit type dependencies in delegate and local-function signatures

**Introduced by:** [`c167316`](https://github.com/ATrefzer/CSharpCodeAnalyst/commit/c167316819fdb5a25587bc97e32a99d57ebc3a1c).
**Implementation:** `DeclarationAnalyzer.AnalyzeDelegateRelationships`,
`SyntaxWalkerBase.VisitLocalFunctionStatement`.

### Examples and Roslyn shapes

~~~csharp
interface IMarker { }
delegate void Callback<T>() where T : IMarker;
~~~

~~~text
DelegateDeclaration "Callback"
├─ TypeParameterList → TypeParameter "T"
└─ TypeParameterConstraintClause
   ├─ Name: IdentifierName "T"
   └─ TypeConstraint → IdentifierName "IMarker"
~~~

~~~csharp
// Inside Run:
Token Local(Token value) => value;
~~~

~~~text
LocalFunctionStatement "Local"
├─ ReturnType: IdentifierName "Token"
├─ ParameterList
│  └─ Parameter "value"
│     └─ Type: IdentifierName "Token"
└─ ArrowExpressionClause → IdentifierName "value"
~~~

### Previous failure and graph decision

Delegate parameters and returns were handled, but delegate constraints were not. Local functions
have no graph elements; merely traversing their bodies did not reliably record their signature types.

Delegates now use the existing constraint helper: `Callback<T> Uses IMarker`. Local-function
parameter, return and constraint types contribute `Uses` edges to the owning member, for example
`Run Uses Token`. This also applies to nested functions and functions inside lambdas.

No local-function, parameter or return nodes are introduced. Special constraints such as
`class`/`new()` contribute no type edges. Local-function body traversal is unchanged; these edges
describe declared dependencies even if the function is never invoked.

**Regression coverage:** [DeclarativeTypeDependencyParseTests](../../Tests/UnitTests/Parser/DeclarativeTypeDependencyParseTests.cs)
covers simple/generic/multiple constraints, arrays, returns, nesting, lambdas, special constraints and
the absence of local-function nodes.

## Signature attributes are dependencies, not owner annotations

**Introduced by:** [`8223391`](https://github.com/ATrefzer/CSharpCodeAnalyst/commit/8223391674f5cf66d43aa9e0f341b9a934afc929).
**Implementation:** `DeclarationAnalyzer.AnalyzeSignatureAttributes`,
the attribute-data overload of `AnalyzeAttributeRelationships`, and property signature handling.

### Example and Roslyn shape

~~~csharp
[return: Mark(typeof(Token))]
int Run([Mark(typeof(Token))] int value) => value;
~~~

~~~text
MethodDeclaration "Run"
├─ AttributeList
│  ├─ AttributeTargetSpecifier (identifier token: return)
│  └─ Attribute "Mark"
│     └─ AttributeArgumentList
│        └─ AttributeArgument → TypeOfExpression → IdentifierName "Token"
├─ ParameterList
│  └─ Parameter "value"
│     └─ AttributeList
│        └─ Attribute "Mark"
│           └─ AttributeArgumentList
│              └─ AttributeArgument → TypeOfExpression → IdentifierName "Token"
└─ ArrowExpressionClause → IdentifierName "value"
~~~

The attributes belong to different semantic sites:
`method.GetReturnTypeAttributes()` and `parameter.GetAttributes()`, rather than the method's own
`GetAttributes()` collection.

### Previous failure and graph decision

Only examining the owning element's attributes lost these attribute-type dependencies.
The existing attribute analysis is reused with ownership and annotation metadata separated:

- `Run UsesAttribute MarkAttribute` records both application locations.
- `Run Uses Token` retains the `typeof` dependency.
- The existing graph convention records an explicit internal attribute constructor as `Calls`;
  named property arguments retain setter references.
- `Run.Attributes` does not acquire `MarkAttribute` merely because a parameter or return is annotated.
  An annotation directly on `Run` still appears there.

This graph convention describes dependencies; an attribute constructor reference is not a claim
that the owning method eagerly constructs the attribute on each invocation.

| Attribute site | Existing graph owner |
|---|---|
| Method/constructor parameter or method return | Method/constructor |
| Delegate parameter or return | Delegate |
| Indexer parameter | Property/indexer container |
| Getter return | Getter accessor |
| Setter's final value parameter | Setter accessor |

Property accessor elements are deliberately absent from `ElementIdToSymbolMap`; the general phase-2
method loop skips them. Property analysis therefore supplies their signature handling. The setter
path visits only its final value parameter, not copied indexer parameters, so indexer annotations do
not automatically spread to both accessors.

**Regression coverage:** [SignatureAttributeParseTests](../../Tests/UnitTests/Parser/SignatureAttributeParseTests.cs)
covers the ownership sites above, partial methods, record primary constructors, distinct source
locations, argument dependencies and preservation of direct-owner annotation metadata.

**Boundary:** attributes on local-function/lambda signatures, type parameters, assemblies and modules
are not expanded by this pass. There are no new parameter/return nodes.

## Graph invariants and deliberate coverage boundaries

**Introduced by:** [`bc8f3b0`](https://github.com/ATrefzer/CSharpCodeAnalyst/commit/bc8f3b07be2dc659fe5046ba69c73db5c73a4657).
**Implementation:** test-only `GraphInvariantAssertions`, the shared in-memory fixture base and
explicit repository smoke tests.

A syntactically valid input can still expose a graph-construction error. For example, the
`GlobalStatement → InvocationExpression` tree shown above previously led to an external edge target
that was never inserted. Syntax coverage alone does not establish graph integrity.

The shared fixture base now checks correct source ownership and existing targets for every edge,
registered parent/child object identity, reciprocal containment, assembly roots and absence of
containment cycles. `ParserGraphInvariantTests` combines top-level statements, generics, a record,
events and attributes, with externals both enabled and disabled.

`ParserRepositorySmokeTests` provides opt-in MSBuild imports of the real CodeGraph and CodeParser
projects. These need a restored checkout and working SDK/build-host communication. They are not a
claim that every environment can load those projects successfully.

See [parser-architecture.md](parser-architecture.md#coverage-boundary-and-regression-checks) for commands
and scope. The graph remains a source-dependency model, not a complete runtime call graph; an
unreferenced element is a candidate for investigation, not proof of dead code.
