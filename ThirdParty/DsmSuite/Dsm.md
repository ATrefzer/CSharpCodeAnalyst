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

**Note that this is the opposite convention of tools like NDepend.**

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

## Cell colours

| Colour | Meaning |
|---|---|
| Background (light neutral) | no relation, and not inside an expanded block |
| Depth ramp `MatrixColor1..4` | inside an expanded element's block, shade = nesting depth |
| `MatrixColorCycle` (warm orange) | **the two elements depend on each other** |

The cycle colour overwrites everything else, which is why it is the loudest colour in our palette. In the

Hovering or selecting a row/column multiplies the cell colour by 1.1 / 1.2 (`MatrixTheme.GetHighlightBrush`)
to draw the crosshair.

## Inside a cell: the number

The **number** is the dependency weight: the count of distinct type-level edges aggregated under the two
elements. Above `9999` it reads `>9K`, because that is the widest that fits — the exact value is in the
cell's tooltip.

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
- **Pass-through namespaces are dropped** — a namespace with exactly one child, itself a namespace, holds no
  types, so its row and column are an exact duplicate of its child's. Note the consequence: a row then reads
  `History` where the real namespace is `CSharpCodeAnalyst.History`.
- **Rows and columns are partitioned** (`PartitionSortAlgorithm`) so an acyclic structure actually comes out
  triangular. Without it the order is meaningless and a layered design looks as tangled as a knot.
- **All weights are currently 1** per distinct type-level edge, not the number of underlying relationships.
  The numbers you see on a collapsed element are sums of those.

## The metrics panel is hidden

DsmSuite has a metrics column between the row headers and the cells, reachable through the arrow in the top
left corner. Both are hidden — its numbers contradict the application's own system metrics. The reasoning is
in [README.md](README.md)

## Controls

- **Ctrl + mouse wheel** zooms the whole matrix (0.04 – 4.0). Plain wheel scrolls.
- **Click a row header** to select it — this is what drives the indicator bars. Clicking a column header
  selects the column but clears them.
- **Click the arrow** in a row header (the top-left 20×24 px) to expand or collapse; hold **shift** and it
  works recursively.

The view is read-only. Everything upstream offers that edits the DSM model — the context menus, dragging a
row header onto another to re-parent it — is removed, because the model here is a projection of a parsed
code graph and an edited row would no longer say anything about the code. See [README.md](README.md).
