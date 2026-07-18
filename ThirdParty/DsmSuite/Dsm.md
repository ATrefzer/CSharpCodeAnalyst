# Reading the DSM

What the matrix on the **DSM** tab draws and how to read it. Almost none of this is discoverable from the
UI — the legend at the bottom covers three of the colours and nothing else — and the rest is spread over
`MatrixCellsView.OnRender`, `MatrixRowHeaderItemView.OnRender` and `MatrixViewModel.DefineCellColors`.

For what we feed in, see `Features/DsmMatrix/CodeGraphToDsmModelBuilder.cs`; for our changes to the viewer
itself, see [README.md](README.md).

## The axes

> **Row = provider. Column = consumer.** A number at (row R, column C) means: **C depends on R.**

```csharp
IDsmElement consumer = _elementViewModelLeafs[column].Element;
IDsmElement provider = _elementViewModelLeafs[row].Element;
int weight = _application.GetDependencyWeight(consumer, provider);
_cellWeights[row].Add(weight);
```

This is **not** the opposite of NDepend, which is the easy assumption to make. Their documentation says
verbatim: *"Blue cell means that the element in column uses the element in row"* — the same convention. What
NDepend adds is the mirror: *"Green cell means that the element in row uses the element in column"*, so every
dependency appears twice, blue below the diagonal and green above it, and a mutual pair is black in both.
That is why their layered example has "all blue cells in the lower-left triangle and all green cells in the
upper-right".

Here each dependency is drawn **once**, so only one reading direction is populated: top to left. The mirror
would cost the cell colour, which is spent on nesting depth (see below) — NDepend spends it on direction and
has no depth-coloured blocks.

### The order number

Every row shows a number, and every column header repeats it. It is `IDsmElement.Order`, assigned 1..N
across the whole flattened tree — that is the cross reference between a column and its row. It is not
stable across runs: expanding or collapsing re-assigns it.

## The diagonal and the darker squares

Expanding an element paints the square block on the diagonal that spans all of its leaves in that element's
**nesting depth colour** (`MatrixColorConverter.GetColor(depth)`, our ramp in
`Features/DsmMatrix/DsmMatrixTheme.xaml`, darker with depth). The diagonal cell of each leaf gets the same
treatment.

This is the single most useful reading aid in the whole view:

> **Inside the square = internal to that assembly or namespace. Outside = crosses its boundary.**

The block is drawn once from its top-left corner (`parent.Children[0] == child`); the root gets none
(`Depth > 0`).

**There are only four depth colours, and they cycle:**

```csharp
public static MatrixColor GetColor(int depth)
{
    switch (depth % 4) { ... }   // Color1 .. Color4
}
```

So the shade is not the depth, it is the depth modulo four — nest five levels deep and the innermost block
is painted like the outermost. That makes the ramp a scarce resource: every nesting level that carries no
structure of its own still burns one of the four, shifts the rest, and brings the wrap-around one level
closer. It is the main reason the builder drops pass-through namespaces (see below), and worth remembering
before adding a level to the hierarchy we feed in.

## Cell colours

| Colour | Meaning |
|---|---|
| Background (light neutral) | no relation, and not inside an expanded block |
| Depth ramp `MatrixColor1..4` | inside an expanded element's block, shade = nesting depth |
| `MatrixColorCycle` (warm orange) | **the two elements depend on each other** |

The cycle colour overwrites everything else, which is why it is the loudest colour in our palette.

Hovering or selecting a row/column multiplies the cell colour by 1.1 / 1.2 (`MatrixTheme.GetHighlightBrush`)
to draw the crosshair.

## Inside a cell: the number

The **number** is the dependency weight: the count of distinct type-level edges aggregated under the two
elements. Above `9999` it reads `>9K`, because that is the widest that fits — the exact value is in the
cell's tooltip.

Below roughly a third of full zoom the number stops fitting the cell altogether and is not drawn. A
populated cell and an empty one then look the same, and the tooltip is the only thing that still tells
them apart — which is why it carries the weight.

It is drawn smaller than the rest of the matrix (font size 10 against 14). That is not decoration: at 14 a
cell only has room for three digits, and upstream silently dropped the fourth, drawing `1000` as `100`. See
[README.md](README.md).

> **Fully expanded, every populated cell reads `1`.** `TypeGraph` deduplicates, so there is exactly one
> edge per pair of types, and we write it with `weight: 1`. `DsmRelationModel.AddWeights` sums along both
> ancestor chains, so aggregation only happens *above* the leaves.

Upstream also drew the weight as a small bar under the number, sized by its decile among all populated
cells. We removed it; see [README.md](README.md).

## The colored bar beside the row headers

The bar at the right edge of a row header, against the matrix, is **relative to the currently selected
row**. It answers "how does this row relate to the thing I clicked", not "what is this row".

It has to be a **row**: `UpdateRelationFlags` reads `SelectedRow`, and `SelectColumn` sets `SelectedRow` to
null. Clicking a column header therefore draws the crosshair but clears the bars. With nothing selected
there are none at all.

`MatrixRowHeaderItemView.GetIndicatorColor()`:

| Colour | Flag | Meaning |
|---|---|---|
| Green | `IsConsumer` | this row **uses** the selected element |
| Blue | `IsProvider` | this row **is used by** the selected element |
| Orange | both | **mutual — a cycle** |

Upstream also drew a second bar at the *left* edge, an addition of this fork, marking the leaves of an
expanded selection that had relations reaching outside it. We removed it; see [README.md](README.md).

## What we put into it

From `CodeGraphToDsmModelBuilder`:

- **One vertex per internal type.** Fine-grained relationships (calls, field access, ...) are lifted to the
  containing type and deduplicated; external types and self edges are dropped (`TypeGraph`).
- **The hierarchy comes from the code graph's parent chain**, not from splitting dotted names. Assembly
  names keep their dots.
- **Pass-through namespaces are merged into one level** — a namespace whose only child is another namespace
  holds no types of its own. The parser creates one element per namespace *segment*, so every project whose
  root namespace repeats its assembly name grows such a chain: assembly `CSharpCodeAnalyst.CodeGraph` holds
  namespace `CSharpCodeAnalyst` holds namespace `CodeGraph` holds the real ones. Without this, eight
  assemblies gave eight rows all reading `CSharpCodeAnalyst`. Each level costs an expand that reveals a
  single row carrying the same numbers as before, a vertical strip, and one of the only four depth colours.

  The chain becomes one element carrying all of their names, so the row reads `CSharpCodeAnalyst.CodeGraph`
  — the namespace that exists — rather than `CodeGraph`, which names nothing. A label is always the namespace
  path relative to the element it sits in: one segment where nothing was collapsed (`Algorithms` inside
  `CSharpCodeAnalyst.CodeGraph`), the whole collapsed chain where something was.
- **Rows and columns are partitioned** (`PartitionSortAlgorithm`) so an acyclic structure actually comes out
  triangular. Without it the order is meaningless and a layered design looks as tangled as a knot.
- **All weights are currently 1** per distinct type-level edge, not the number of underlying relationships.
  The numbers you see on a collapsed element are sums of those.

## The metrics panel is hidden

DsmSuite has a metrics column between the row headers and the cells, reachable through the arrow in the top
left corner. Both are hidden — its numbers contradict the application's own system metrics. The reasoning is
in [README.md](README.md).

## Controls

- **Ctrl + mouse wheel** zooms the whole matrix (0.04 – 4.0).
- **Plain wheel** scrolls up and down, **shift + wheel** sideways — from anywhere in the matrix, the headers
  included. Worth knowing because the scroll bars sit *inside* the scaled grid, so zooming out shrinks them
  along with everything else until they are too thin to grab. The wheel is unaffected: it moves by a fixed
  number of content units, which is the same couple of cells at any zoom.
- **Click a row header** to select it — this is what drives the indicator bars. Clicking a column header
  selects the column but clears them.
- **Click the arrow** in a row header (the top-left 20×24 px) to expand or collapse; hold **shift** and it
  works recursively.
- **Hover a cell** for the consumer's and the provider's full names plus the weight; **hover a row or
  column header** for the element's full name and type. Neither shows an element id: DsmSuite numbers its
  own model elements, and that number means nothing outside it.

The view is read-only. Everything upstream offers that edits the DSM model — the context menus, dragging a
row header onto another to re-parent it — is removed, because the model here is a projection of a parsed
code graph and an edited row would no longer say anything about the code. See [README.md](README.md).
