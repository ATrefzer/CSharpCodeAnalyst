# Corrections and updates

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

To fix this each property is split into separate `get_Prop` / `set_Prop` accessor nodes. This is gated behind the `SplitPropertyAccessors` parser option (on by default, because cycle accuracy is the main goal).

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

Same guard as the normal path (see `AnalyzeObjectCreation`): only explicit, internal constructors get the edge; implicit/primary/external constructors are already covered by the type `Uses`.



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

A consequence worth knowing (observed on the Jellyfin reference repo, accepted deliberately): a **named attribute argument** like `[JellyfinMigrationBackup(JellyfinDb = true)]` runs through the normal property-access classification and therefore yields a `Calls` edge from the decorated element to the attribute's *property* (`JellyfinDb`), classified as a write. That is technically accurate - instantiating the attribute really does set the property - even though no user code spells out the call. We keep it: special-casing attribute arguments to `Uses` would add a context check for little gain, and the edge makes attribute properties visible as used.

**Enum member initializers.** Enum members are deliberately not code elements, but that also meant `enum Level { Highest = Limits.Max }` was never walked. The initializer expressions are now walked with the dependencies anchored on the enum element itself. Note: a member referencing another member of the same enum (`All = A | B`) falls back to the containing type and yields a self-edge - consistent with recursive methods.

**Primary-constructor base-call arguments.** `class Derived() : Base(Helper.DefaultSize())` - the primary constructor has no method element and type declarations have no body walk, so the argument expressions were lost (with a classic `: base(...)` they are part of the walked constructor declaration). The arguments are now walked anchored on the type element, consistent with the primary-constructor parameter types; the call to the base constructor itself gets a `Calls` edge with `IsBaseCall`, same guard as constructor initializers (explicit, internal constructors only).

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

Two members were found only through this: a **static constructor** (`.cctor`) and a **finalizer** (the
destructor arrives from the parser as an ordinary method named `Finalize` - the Roslyn symbol name). Both
are run by the runtime, can never be referenced from code, and are effectively private, so they landed in
the highest confidence band. The static constructor started out annotated as an entry point; both are now
dropped from the result entirely - on a live type such a row is wrong in every case, and on a dead type
the roll-up covers them.
