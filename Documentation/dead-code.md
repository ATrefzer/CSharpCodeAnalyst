# Dead Code

[TOC]

This guide explains the **Dead Code** analysis: what it reports, what it deliberately does not report, and
how much you can trust the result.

Available via *Analyzers → Dead Code*. The result is a sortable table:

| Column  | Meaning                                                                     |
| ------- | ---------------------------------------------------------------------------- |
| Element | The fully qualified name of the unreferenced element.                        |
| Kind    | Class, Interface, Method, Field, Property, ... — the kind of element.        |
| Access  | The element's visibility, empty when the producer does not supply one.        |
| Confidence | How much the finding can be trusted — coloured like the complexity metric. See below. |
| Notes   | Anything worth knowing about the finding. **Empty means nothing speaks against deleting it.** |

Sort by *Notes* to get the clean cases together, and use *Jump to code* or *Copy to explorer graph* from
the context menu to check a finding.

> **Two rows can carry the same name.** A full name is built from the plain symbol names, which carry
> neither generic arity nor a parameter list. So `WpfCommand` and `WpfCommand<T>` read alike, and so do the
> overloads `Foo(int)` and `Foo(string)`. They are separate elements in the graph — only the display
> collides, and one of them being dead while the other is used looks like a wrong finding. *Jump to code*
> resolves it: the source locations differ.

On a large codebase the fastest way to make the result readable is the filter box, which understands the
same expressions as the Advanced Search — including **exclusion** with a leading `-`. Whole groups of
findings disappear at once:

```
-Strings. -Tests -ThirdParty
```

`-type:property` drops a whole element kind, and `-source:extern` works as well. Terms combine with spaces
(AND) and `|` (OR); the exclusion belongs to the term it precedes.

The *Notes* column carries two different kinds of remark, which is why it is not called something like
"might still be used":

- A **doubt** — `Entry point`, `Test code`, `Attributes: ...`. The reference may exist somewhere the parser
  cannot see, so the finding needs a second look.
- An **explanation** — `Implemented but never called: ...`, `Implements unused contract: ...`. Those are
  the opposite of a doubt: the finding is well understood, and the note tells you what dies together with
  it.

## The rule

An element is dead when **no relationship enters its subtree from the outside**.

The subtree is the point. Two things follow from it, and both match how you would judge the code by hand:

- A class is alive when one of its members is used, even if nothing ever names the class itself. Somebody
  calls `Service.Run()`, so `Service` is not dead code — you cannot delete it.
- A class cannot keep itself alive. If its methods only call each other, every one of those calls stays
  inside the subtree and proves nothing. The whole class is dead.

Because a dead element implies a dead subtree, **only the topmost dead element is reported**. If a class is
dead you get one row for the class, not one row per method. If the class is alive but three of its methods
are unused, you get those three rows.

Namespaces and assemblies are never reported: nothing ever references them in the graph, so they would all
look dead. Code from outside the solution (frameworks, NuGet packages) is out of scope — we see neither its
callers nor its body.

### One round, not a chain

The analysis reports what nothing references **right now**. It does not chase the consequences: if the only
caller of `Formatter` sits in the `Report` class you just got reported, `Formatter` is *not* also reported —
it is referenced, by dead code, but referenced.

That is a deliberate step back from an earlier cascading version, which kept re-running with the previous
findings switched off. It worked, but it multiplied every false positive: one invisible `{Binding}` took
seven further elements with it, and the deeper rounds were only as good as the rounds below them.

**Delete a finding and run the analysis again.** You get the next layer, one honest round at a time, and
every row stands on its own.

### Code only the tests still use

References are counted in two colours: those from test code, and those from everything else. An element
with no reference at all is the obvious finding. An element that only *tests* reference is the same
statement about the production code, and is reported with the note `Used only by tests`.

Without this rule, analyzing the tests along with the production code would **hide** dead code. Exclude the
test projects and the elements show up as unreferenced; include them and the tests hold them up. Both
answers cannot be right, and this is the one that survives either setup.

**Test code is decided per type:** a type is a test type when it, or anything inside it, carries a known
test-framework attribute — the subtree, because xUnit has no class-level attribute (only the `[Fact]`
methods carry one) and NUnit finds classes without `[TestFixture]` too. Attribute names are matched
case-sensitively; a domain attribute that happens to be called `test` does not count.

Per type and not per assembly (which it used to be), because one embedded test class poisoned its whole
assembly in both directions: every reference leaving the assembly counted as a test reference, so code in
*other* assemblies used from ordinary production code was falsely `Used only by tests` — and production
code beside the embedded tests, used only by them, was never found, because the whole assembly was exempt.

**Inside a test type the rule does not apply.** A fixture's own helper members and nested fakes are what
the tests are made of. A helper *class* outside the fixtures carries no attribute, though, so it is
production code to the analysis and shows up as used-only-by-tests. That is the accepted price of the type
granularity, and the statement is true — the helper goes when the tests go — it is just noise while the
helper is doing its job.

The note names the tests that reference the element — what has to go with it. It can be empty: when the
element is reached through a contract member (`ICommand.Execute` and friends) there is no edge whose caller
we could name.

> This needs the test projects to be part of the analysis. The default project exclusion filter is
> `.*Tests`, which removes them — then there is no test code, and the rule finds nothing.

### Generated code

Code a tool wrote is **always** parsed and never excluded. That is not a convenience, it is a correctness
requirement: generated code holds relationships nothing else does.

- The markup compiler's `Connect` in `MainWindow.g.cs` is the **only** caller of every XAML event handler.
- A CommunityToolkit `[ObservableProperty]` generates the only code that reads the backing field, and
  `[RelayCommand]` the only code that calls the method behind it.

Leave that out and you do not merely hide the generated members — you turn the hand-written code they
reference into dead code. There used to be an *Include generated code* setting; it is gone, because no
answer it could give was right.

Instead every element a tool wrote is **marked** (`CodeElement.IsGenerated`) and its findings carry the
note `Generated code`. In this repository that is 812 of 8473 elements and 83 of the findings — resource
designer members, `IComponentConnector.Connect`, the fields behind `x:Name`.

**The marking asks about every declaration, not any.** A WPF code-behind class is `partial`: half of it is
`MainWindow.xaml.cs`, which you wrote, half is `MainWindow.g.cs`, which the markup compiler wrote — one
element, two source locations. An element counts as generated only when *all* of its declarations sit in
generated files, so your code-behind class stays yours while `Connect` and the `x:Name` fields, which
exist nowhere else, are marked.

Recognized the way Roslyn's own analyzers do it: the file name (`.g.cs`, `.g.i.cs`, `.designer.cs`,
`.generated.cs`, `.AssemblyAttributes.cs`, `TemporaryGeneratedFile_*`) or an `<auto-generated>` comment
before the first token. Source-generated documents are generated by definition.

### Suppressed: serialized properties

**A public property of a type carrying a serialization attribute** is not reported. The serializer reaches
it by reflection, so "nothing references it" says nothing about it — and on such a type that would be the
rule rather than the exception, filling the result with rows nobody can act on.

Recognized attributes (the ones that mark the whole type): `[Serializable]`, `[DataContract]`,
`[JsonObject]`, `[JsonConverter]`, `[XmlRoot]`, `[XmlType]`, `[ProtoContract]`, `[MessagePackObject]`. None
of them is inherited in C#, so a derived DTO has to carry its own — and a plain DTO without any attribute
(`System.Text.Json` needs none) is not covered.

Two boundaries are deliberate:

- **Only properties, and only public ones.** A private property, a method or a field on the same type is
  reported as usual — the serializer resolves by public reflection and reaches none of them. (`[Serializable]`
  with `BinaryFormatter` does serialize fields; that case is not covered.)
- **Only the member.** If the whole class is dead, the class is reported — carrying a serialization
  attribute is not a use of the type.

What you lose on those types is the finding "this property is written but never read".

> An earlier version dropped a **single property accessor** as well, so a setter nobody calls never showed
> up and only a property dead as a whole was reported. It halved the output, but it answered a question
> nobody asked — "is this property used at all" instead of "is anything reading it" — and it contradicted
> the rest of the analysis, which reports and annotates rather than drops. Accessors are reported again.
> They only exist in the graph when the parser option **Split property accessors** is on.

### Which relationships count as a reference

`Calls`, `Creates`, `Uses`, `Inherits`, `Invokes`, `UsesAttribute`, and `Implements` between two *types*
(`class C : IFoo` names `IFoo` in C's declaration).

`Containment` is the parent/child hierarchy, not a use. `Bundled` is an artificial edge the graph view
creates. `Handles` points from the handler to the event and is the callback wiring, not a dependency — but
nothing is lost: registering `x.Click += OnClick` also produces a method-group `Uses` edge that keeps
`OnClick` alive.

## Interfaces, overrides and abstract members

`Implements` and `Overrides` between two *members* point from the implementation to the contract. Taken
literally that gives two wrong answers: an implementation never has an incoming reference and would always
look dead, while a contract member always looks used just because somebody implements it.

So those edges are not counted as references. Instead **liveness is propagated the other way**:

- A contract member that *is* called keeps every implementation alive — and the types holding them.
  Calling `IService.Run()` is a call to `Service.Run()`, even if nothing else ever mentions `Service`.
- A contract member that is *never* called dies together with its implementations. You get one row for the
  contract (`Implemented but never called: ...`) and one for each implementation (`Implements unused
  contract: ...`). That pair is one of the more valuable findings: an abstraction nobody uses.

Contracts from **outside** the solution are the exception. We cannot see who calls `IDisposable.Dispose` or
`object.ToString`, so an implementation of them is assumed to be alive and is not reported. That assumption
deliberately stops at the member: **implementing `IDisposable` is not a use of the class.** A class whose
only remaining trace is a `Dispose` method is still reported as dead.

The graph itself cannot carry that fact. With *Include External Code* off — the default — the parser records
no `Implements` / `Overrides` relationship for a contract outside the solution, because there is no element
to point at. With it on, the edge is flattened to a `Uses` relationship on the containing *type*, which is
indistinguishable from ordinary use of that type.

So the parser records it **beside** the graph instead, from the symbols, the same way it does for source
metrics. Those members are still listed — with `Implements external contract: ICommand.Execute` in the
*Notes* column, so the judgement stays visible instead of rows disappearing silently.

## Confidence

Three levels, each from one stated rule, evaluated in this order. It is a summary of what is known, not a
measurement:

| Confidence | Rule |
| ---------- | ------ |
| **Low** (red) | The finding carries a note saying the caller may sit outside the graph — entry point, test code, attributes, an external contract. We already know we might be wrong. |
| **High** (green) | No such note, and the element **or one of its containers** is `private` or `internal`. Nothing outside the analyzed code could reach it, so "nothing references it" and "nothing *can* reference it" are the same statement. |
| **Medium** (orange) | Everything else: `public`, `protected`, or an unknown visibility. |

The containment part matters more than it looks: a `public` method of an `internal` class cannot be called
from another assembly either, so it still qualifies as high.

**A `Used only by tests` finding is capped at medium.** The ladder measures whether a caller we cannot see
could exist, and that question stays valid — a public element could still be used from an assembly outside
the analysis. High is the one level such a finding must not reach, because high claims that nothing *can*
reference the element while something demonstrably does. Whether a test alone justifies keeping it is a
decision, not a measurement. Low still wins when one of the notes above applies.

**One exception to high:** a `public` property on a type that implements `INotifyPropertyChanged` — a view
model. A XAML `{Binding}` reaches exactly that, and bindings are the one XAML construct the analysis
deliberately does not follow. The parser records every analyzed type with the interface anywhere in its
interface set, no matter which class of the inheritance chain implements it — so a view model deriving from
a base class outside the analyzed code (`ObservableObject`, `BindableBase`, ...) is covered too, although
nothing in the graph itself says it is a view model. A project file saved before this existed has no
recorded types, so the rule is simply off there until the solution is parsed anew — the same graceful
degradation as for the other parser-side facts.

Being confined does not help against this. A public property of an internal class cannot be referenced from
another assembly, but the binding sits *inside* the assembly and is merely invisible — a different thing.
Private, internal and protected properties stay high: the binding engine resolves by public reflection and
cannot reach them.

> **Known gap.** The rule keys on `INotifyPropertyChanged`, so a plain object bound inside a `DataTemplate`
> is not covered. In this repository the `Mru` class (`Path`, `Command`, bound from an `ItemsSource`) is
> exactly that case and still shows as high. Recognizing it would mean demoting *every* public property,
> which costs real findings in non-WPF code.

**Unknown visibility never reaches high.** Every importer except the C# parser leaves it unset, and so does
a project file written before this existed. That is the honest answer rather than a penalty — without
knowing the visibility we cannot claim that nothing outside could reference the element.

The high bucket is deliberately small: it is the list you can work through without checking each entry by
hand.

> `InternalsVisibleTo` is not taken into account. A friend assembly inside the analysis shows its references
> anyway; one outside it is the rare case this misses.

## The notes

The analysis can only see what the parser saw. Everything reached through reflection, dependency injection,
serialization or a test runner therefore looks unreferenced. Those elements are not silently dropped — they
are reported with a note, and you decide. (Two exceptions are dropped outright: the serialized property
above, where the note would be on every row of a DTO, and static constructors and finalizers — no code can
reference those, only the runtime calls them, so the row would be wrong on every live type.)

**Doubts** — the reference may exist where the parser cannot look:

| Note              | Meaning                                                                       |
| ----------------- | ------------------------------------------------------------------------------ |
| `Entry point`     | `Main`, or the synthetic `GlobalStatements` element for top-level statements. |
| `Test code`       | The element or something below it carries a known test-framework attribute.    |
| `Generated code`  | A tool wrote it, not a person. The finding is correct, it is just not for you — the next build writes it again. See [Generated code](#generated-code). |
| `Attributes: ...` | The element carries attributes — often the sign that a framework drives it. Every attribute is listed, including the test ones that already produced `Test code`. |

**Explanations** — the finding is understood, and the note names what dies with it:

| Note                                | Meaning                                                                     |
| ----------------------------------- | ----------------------------------------------------------------------------- |
| `Implemented but never called: ...` | A contract member that is implemented but never called through the contract.  |
| `Implements unused contract: ...`   | Implements or overrides an internal contract member that is itself dead.      |
| `Implements external contract: ...` | Implements or overrides something outside the analyzed code (`ICommand.Execute`, `object.GetHashCode`). The framework is the caller, so this is almost certainly alive. |
| `Used only by tests: ...`           | Nothing in the production code references it — the listed tests are the only thing keeping it alive. See [Code only the tests still use](#code-only-the-tests-still-use). |

Notes are collected over the whole subtree, because the evidence usually sits below what is reported: a
test fixture is reported as a dead *class*, but the `[Test]` attributes are on its methods.

## What XAML the analysis does see

Half of XAML is compiled into C# and is therefore fully visible; the other half is not, and the split is
sharp.

The markup compiler writes a partial class per XAML file (`obj/.../MyView.g.cs`) and that file **is** part of
the compilation — MSBuildWorkspace runs the markup compile pass during its design-time build, so this works
even on a solution that was never built. The generated class contains a field per `x:Name`d element and a
`Connect` method that wires the event handlers:

```csharp
this.CodeTree.ContextMenuOpening += new ContextMenuEventHandler(this.TreeView_ContextMenuOpening);
```

That is ordinary C#, so those references are found like any other. Everything declarative is compiled into
**BAML** instead — a binary resource resolved by reflection at runtime, with no C# generated for it. The
parser closes most of that gap by reading the XAML files themselves and adding the references that carry a
fully qualified CLR name (see `ParserConfig.IncludeXamlReferences`).

| In XAML                                                      | Found? |
| ------------------------------------------------------------ | ------ |
| `Click="Button_Click"`, anywhere incl. templates              | yes, generated C# |
| `x:Name="CodeTree"` in the file's main name scope             | yes, generated field |
| `x:Name` inside `DataTemplate` / `ControlTemplate` / `Style`  | no field is generated — but the element tag is read from the XAML |
| `<local:MyControl/>` — the element tag                        | yes, read from the XAML — including the constructor it runs |
| `{x:Static resx:Strings.Header}`, `{x:Type local:Foo}`        | yes, read from the XAML |
| `{Binding SaveCommand}`                                       | **no** |
| `{StaticResource key}`                                        | **no** |
| `Source="Styles/Buttons.xaml"`, `StartupUri`                  | **no** |

Prefixes are resolved through the `clr-namespace` xmlns declarations, so nothing is matched by guessing.
`{Binding}` is deliberately left out: without evaluating the DataContext it is a bare member name, and
matching that across the codebase would suppress far more than it explains.

The name-scope rule is worth knowing even so. `MainWindow.xaml` in this repository has ten `x:Name`s and
the generated file has nine fields — the missing one sits inside a `DataTemplate`. Before the XAML files
were read, that control looked unused despite being used three times.

A XAML file with no code-behind class (a resource dictionary) is represented by a synthetic class named
after its path, so its references have a source. Nothing resolves the `Source` URIs of merged dictionaries,
so those synthetic elements appear in the result themselves — six of them in this repository.

## Limitations

Read these before deleting anything.

- **The rest of XAML.** Most of it is covered — see the section above for the exact split. What remains
  invisible is `{Binding}`, `{StaticResource}` and the `Source` / `StartupUri` URIs of merged dictionaries.
  Reading the XAML files removed 187 findings on this repository when it was introduced.
- **Reflection, DI and serialization** are invisible for the same reason: the reference only exists at
  runtime. What is handled explicitly are the two suppressed cases above — a single property accessor and
  a public property of a type marked as a serialization target.
- **External contracts are recognized, but only for C#.** The information comes from the Roslyn symbols, so
  a graph produced by one of the importers (Java, C++, Dart, ...) does not have it, and a project file
  written before this existed does not either — parse the solution again to get it.
- **Public API.** Accessibility is not part of the code graph, so a library whose public API is consumed by
  a different solution will report most of that API as dead.
- **Only the analyzed scope.** The analysis is only as complete as the loaded graph. If the solution was
  parsed with project exclusions, or the graph came from an import, the callers may simply be missing.
- **Only one layer at a time.** Code that is kept alive solely by the code just reported does not show up
  in the same run — see *One round, not a chain*. Delete and run again.
- **Dead cycles are not found.** Two classes that only use each other and nothing else each have an
  incoming reference, so neither is reported — and re-running does not help, because nothing ever breaks
  the cycle. Finding those requires reachability from an explicit set of entry points.
