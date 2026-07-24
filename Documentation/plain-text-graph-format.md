# The Plain Text Graph Format

C# Code Analyst can export and import a code graph as a simple, human-readable text file
(**File → Export → Plain text** / **File → Import → Import plain text**). Because almost every
field is optional, the format is also handy in the other direction: you can write a small toy
graph by hand in a minute and load it into the tool — for experiments, screenshots, bug
reports, or to feed a dependency graph to an LLM.

## Minimal example

```text
# Two classes in one namespace. B inherits A, B.Run calls A.Help.
Assembly app
Namespace core  parent=app
Class A  parent=core
Class B  parent=core
Method A.Help  name=Help  parent=A
Method B.Run  name=Run  parent=B

B Inherits A
B.Run Calls A.Help
```

That is a complete, loadable file. Every line is either an **element**, a **relationship**, a
comment (`#`), or blank. The hierarchy (assembly → namespace → class → method) comes from the
`parent=` references; the arrows in the graph come from the relationship lines.

## Element lines

```text
ElementType Id [name=Name] [full=FullName] [parent=ParentId] [external] [attr=A,B]
[loc=File:Line,Column]
```

| Field      | Meaning                                                                                        |
| ---------- | ---------------------------------------------------------------------------------------------- |
| ElementType| One of the types below (case-insensitive). Required.                                           |
| Id         | Unique identifier of the element, referenced by `parent=` and by relationship lines. Required. |
| name=      | Display name. Defaults to the Id.                                                              |
| full=      | Fully qualified name (used in tooltips, search, architectural rules). Defaults to the name.    |
| parent=    | Id of the containing element. Omit for roots (typically the assembly).                         |
| external   | Marks code outside the solution (framework, packages).                                         |
| attr=      | Comma-separated element attributes (e.g. `static`). Rarely needed.                             |
| loc=       | Optional source locations, one per line *directly below* the element: `File:Line,Column`.      |

Element types: `Assembly`, `Namespace`, `Class`, `Interface`, `Struct`, `Record`, `Enum`,
`Method`, `Property`, `PropertyAccessor`, `Field`, `Event`, `Delegate`, `Other`.

## Relationship lines

```text
SourceId RelationshipType TargetId [Attr1,Attr2]
[loc=File:Line,Column]
```

Relationship types: `Calls`, `Creates`, `Uses`, `Inherits`, `Implements`, `Overrides`,
`Invokes` (event invocation), `Handles` (event handler registration), `UsesAttribute`.
(`Containment` and `Bundled` exist internally — do not use them in hand-written files; the
hierarchy is expressed with `parent=`, not with relationships.)

The optional attributes are flags such as `IsStaticCall`, `IsThisCall`, `IsInstanceCall`,
`IsBaseCall`, `IsExtensionMethodCall`, `IsMethodGroup`, `EventRegistration`,
`EventUnregistration`. For toy graphs you can ignore them.

## Rules and pitfalls

- **No spaces inside values.** Lines are split at whitespace, so Ids, names, full names and
  attribute lists must not contain spaces. `name=My Class` will not do what you want.
- **Elements first, then relationships.** A relationship line is only recognized when both of
  its endpoints are already defined. `parent=` may point forward, but keep the simple order
  anyway: all elements, then all relationships (this is also what the exporter writes).
- Keywords are case-insensitive (`class a` works), but the Ids are case-sensitive.
- Ids are free-form strings. Dots are fine (`core.A.Help`) and make files easier to read, but
  they carry no meaning — the hierarchy comes from `parent=` only. Avoid Ids that equal an
  element type name (like an element with Id `Class`); the line auto-detection could then
  misread a later line.
- One quirk to be aware of: an element referencing a `parent=` that does not exist loads
  without a parent (it becomes a root) — there is no error for a typo in a parent Id.

## Tip

To see a full-featured example, load any solution, put something on the canvas, and use
**File → Export → Plain text**. The export is a faithful round trip of the canvas — including
locations and attributes — and shows every field in context.
