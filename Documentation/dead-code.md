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
| Level   | Which round found it. 1 = nothing references it at all. See *The cascade*.    |
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
| **High** (green) | No such note, found in round 1, and the element **or one of its containers** is `private` or `internal`. Nothing outside the analyzed code could reach it, so "nothing references it" and "nothing *can* reference it" are the same statement. |
| **Medium** (orange) | Everything else: `public` or `protected`, an unknown visibility, or anything the cascade found. |

The containment part matters more than it looks: a `public` method of an `internal` class cannot be called
from another assembly either, so it still qualifies as high.

**One exception to high:** a `public` property on a type that implements `INotifyPropertyChanged` — a view
model. A XAML `{Binding}` reaches exactly that, and bindings are the one XAML construct the analysis
deliberately does not follow. The interface is recognized through the `PropertyChanged` event and is spread
down the inheritance edges, so the usual `MyViewModel : ViewModelBase` shape is covered too.

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

On this repository the distribution is 27 high, 537 medium, 478 low out of 1042 findings. The high bucket is
deliberately small: it is the list you can work through without checking each entry by hand.

> `InternalsVisibleTo` is not taken into account. A friend assembly inside the analysis shows its references
> anyway; one outside it is the rare case this misses.

## The cascade

Round 1 finds what nothing references at all. Every following round ignores the outgoing references of
what was already found, so code that is only kept alive by dead code dies with it:

```csharp
class Report                      // nothing references Report          -> level 1
{
    void Print() { Formatter.Format(); }
}

static class Formatter            // only ever used from Report.Print   -> level 2
{
    public static void Format() { }
}
```

The *Level* column says which round a finding comes from, and that is a confidence scale: level 1 stands
on its own, while level 4 only holds if levels 1 to 3 were right.

**Not every finding propagates.** A finding carrying `Entry point`, `Test code`, `Attributes` or
`Implements external contract` is reported but never used as evidence that something else is dead. This is
load-bearing rather than a refinement: the class holding `Main` is a level-1 finding, and letting it
propagate would declare the entire application dead in round 2. The same protection applies when such a
member merely sits *inside* the reported element — a dead class holding an `ICommand.Execute` takes
nothing with it, because that method may well still run.

## The notes

The analysis can only see what the parser saw. Everything reached through reflection, dependency injection,
serialization or a test runner therefore looks unreferenced. Those elements are not silently dropped — they
are reported with a note, and you decide.

**Doubts** — the reference may exist where the parser cannot look:

| Note              | Meaning                                                                       |
| ----------------- | ------------------------------------------------------------------------------ |
| `Entry point`     | `Main`, a static constructor (the runtime runs it), or the synthetic `GlobalStatements` element for top-level statements. |
| `Test code`       | The element or something below it carries a known test-framework attribute.    |
| `Attributes: ...` | The element carries attributes — often the sign that a framework drives it. Every attribute is listed, including the test ones that already produced `Test code`. |

**Explanations** — the finding is understood, and the note names what dies with it:

| Note                                | Meaning                                                                     |
| ----------------------------------- | ----------------------------------------------------------------------------- |
| `Implemented but never called: ...` | A contract member that is implemented but never called through the contract.  |
| `Implements unused contract: ...`   | Implements or overrides an internal contract member that is itself dead.      |
| `Implements external contract: ...` | Implements or overrides something outside the analyzed code (`ICommand.Execute`, `object.GetHashCode`). The framework is the caller, so this is almost certainly alive. |

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
  Reading the XAML files removed 187 of 1051 findings on this repository.
- **Reflection, DI and serialization** are invisible for the same reason: the reference only exists at
  runtime.
- **External contracts are recognized, but only for C#.** The information comes from the Roslyn symbols, so
  a graph produced by one of the importers (Java, C++, Dart, ...) does not have it, and a project file
  written before this existed does not either — parse the solution again to get it.
- **Public API.** Accessibility is not part of the code graph, so a library whose public API is consumed by
  a different solution will report most of that API as dead.
- **Only the analyzed scope.** The analysis is only as complete as the loaded graph. If the solution was
  parsed with project exclusions, or the graph came from an import, the callers may simply be missing.
- **The cascade amplifies the blind spots.** A false positive in round 1 drags everything it uses into
  round 2. A single `{Binding}`-only property in this repository takes seven resource strings with it. The
  *Level* column is there to make that visible: level 1 stands on its own, everything above it inherits
  the uncertainty of the rounds below.
- **Dead cycles are not found.** Two classes that only use each other and nothing else each have an
  incoming reference, so neither is reported and no round removes them. Finding those requires
  reachability from an explicit set of entry points rather than a cascade.
