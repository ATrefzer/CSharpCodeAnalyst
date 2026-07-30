# Dead Code

[TOC]

This guide explains the **Dead Code** analysis: what it reports, what it deliberately does not report, and
how much you can trust the result.

Available via *Analyzers → Dead Code*. The result is a sortable table:

| Column              | Meaning                                                                                  |
| ------------------- | ---------------------------------------------------------------------------------------- |
| Element             | The fully qualified name of the unreferenced element.                                      |
| Kind                | Class, Interface, Method, Field, Property, ... — the kind of element.                      |
| Might still be used | Why the element could be alive anyway. **Empty means nothing speaks against deleting it.** |

Sort by the hint column to get the clean cases together at the top, and use *Jump to code* or *Copy to
explorer graph* from the context menu to check a finding.

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

> **This only works when the graph contains the edge.** With *Include External Code* switched off — the
> default — the parser records no `Implements` / `Overrides` relationship at all for a contract that lives
> outside the solution, because there is no element to point at. So `ToString`, `GetHashCode`,
> `ICommand.Execute`, a `SyntaxWalker.Visit...` override and friends **are** reported as dead, without a
> hint. Recognizing them would require the parser to remember the fact; see the limitations below.

## The hints

The analysis can only see what the parser saw. Everything reached through XAML, reflection, dependency
injection, serialization or a test runner therefore looks unreferenced. Those elements are not silently
dropped — they are reported with a hint, and you decide:

| Hint                              | Meaning                                                                            |
| --------------------------------- | ---------------------------------------------------------------------------------- |
| `Entry point`                     | `Main`, or the synthetic `GlobalStatements` element for top-level statements.        |
| `Test code`                       | The element or something below it carries a known test-framework attribute.          |
| `Attributes: ...`                 | The element carries attributes — often the sign that a framework drives it.          |
| `Implemented but never called: ...` | A contract member that is implemented but never called through the contract.        |
| `Implements unused contract: ...` | Implements or overrides an internal contract member that is itself dead.             |

The hints are collected over the whole subtree, because the evidence usually sits below what is reported:
a test fixture is reported as a dead *class*, but the `[Test]` attributes are on its methods.

## What XAML the analysis does see

Half of XAML is compiled into C# and is therefore fully visible; the other half is not, and the split is
sharp.

The markup compiler writes a partial class per XAML file (`obj/.../MyView.g.cs`) and that file **is** part of
the compilation. It contains a field per `x:Name`d element and a `Connect` method that wires the event
handlers:

```csharp
this.CodeTree.ContextMenuOpening += new ContextMenuEventHandler(this.TreeView_ContextMenuOpening);
```

That is ordinary C#, so **event handlers declared in XAML and `x:Name`d controls are found** like any other
reference.

Everything declarative is compiled into **BAML** instead — a binary resource that is resolved by reflection
at runtime. No C# is generated for it, so there is no compile-time reference to see:

| In XAML                              | Visible? |
| ------------------------------------ | -------- |
| `Click="Button_Click"`               | yes, via `Connect` |
| `x:Name="CodeTree"`                  | yes, generated field |
| `{Binding SaveCommand}`              | no |
| `{x:Static resx:Strings.Header}`     | no |
| `{StaticResource myConverter}`       | no |
| `{x:Type local:Foo}`                 | no |
| `<local:MyControl/>` without `x:Name` | no |

In this repository the app project alone contains 217 `{x:Static}` usages, and none of them appears in any
generated file. That single category is the largest block of false positives.

## Limitations

Read these before deleting anything.

- **Declarative XAML references.** Not all of XAML is invisible — see below. What is invisible is
  everything declarative: `{Binding}`, `{x:Static}`, `{StaticResource}`, `{x:Type}` and the instantiation of
  a control that has no `x:Name`. Running the analysis on this repository itself, roughly a quarter of all
  findings were resource designer properties referenced from XAML via `{x:Static}`.
- **Reflection, DI and serialization** are invisible for the same reason: the reference only exists at
  runtime.
- **Overrides of framework members are not recognized.** As described above, the graph carries no edge for
  them unless *Include External Code* is on — and even then the parser records the edge as a plain `Uses`
  relationship, which is indistinguishable from an ordinary use. Recognizing these would mean giving the
  parser a way to mark "this member implements something external" on the element itself.
- **Public API.** Accessibility is not part of the code graph, so a library whose public API is consumed by
  a different solution will report most of that API as dead.
- **Only the analyzed scope.** The analysis is only as complete as the loaded graph. If the solution was
  parsed with project exclusions, or the graph came from an import, the callers may simply be missing.
- **No cascade.** This is the direct variant: an element counts as alive as soon as *anything* references
  it — even something that is itself dead. So a dead class keeps the interface it implements and the
  helpers it calls alive. Delete the reported elements and run the analysis again to peel off the next
  layer.
- **Dead cycles are not found.** Two classes that only use each other and nothing else each have an
  incoming reference, so neither is reported. Finding those requires reachability from an explicit set of
  entry points.
