# Reading the DSM

What the matrix on the **DSM** tab draws and how to read it. Almost none of this is discoverable from the
UI — the legend at the bottom covers three of the colours and nothing else — and the rest is spread over
`MatrixCellsView.OnRender`, `MatrixRowHeaderItemView.OnRender` and `MatrixViewModel.DefineCellColors`.
Written down here so it does not have to be re-read out of the drawing code every time.

For what we feed in, see `Features/DsmMatrix/CodeGraphToDsmModelBuilder.cs`; for our changes to the viewer
itself, see [README.md](README.md).

The examples below all refer to one screenshot of this repository's own code graph, expanded to the
namespace level.

## The axes

> **Row = provider. Column = consumer.** A number at (row R, column C) means: **C depends on R.**

```csharp
IDsmElement consumer = _elementViewModelLeafs[column].Element;
IDsmElement provider = _elementViewModelLeafs[row].Element;
int weight = _application.GetDependencyWeight(consumer, provider);
_cellWeights[row].Add(weight);
```

This trips people up, so here is the sanity check: `CSharpCodeAnalyst.CodeGraph` sits at the bottom with a
full row (`1  148  85  11  47`) and an empty column. It is the foundation everything depends on. If rows
were consumers, that picture would mean CodeGraph depends on everything.

Both axes carry the same elements in the same order, so the matrix is square and symmetric in layout, not
in content.

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

A cycle inside the square is an internal tangle of that component. A cycle outside it is one that crosses a
component boundary — the expensive kind. In the screenshot the `CSharpCodeAnalyst.TreeMap` block is the
large square, and the `TreeMap`↔`Common` cycle sits inside it.

The block is drawn once from its top-left corner (`parent.Children[0] == child`); the root gets none
(`Depth > 0`).

## Cell colours

| Colour | Meaning |
|---|---|
| Background (light neutral) | no relation, and not inside an expanded block |
| Depth ramp `MatrixColor1..4` | inside an expanded element's block, shade = nesting depth |
| `MatrixColorCycle` (warm orange) | **the two elements depend on each other** |

The cycle colour overwrites everything else, which is why it is the loudest colour in our palette. In the
screenshot `TreeMap` (421) and `Common` (427) are orange in both directions — `2` one way, `4` the other.
That is a genuine mutual dependency, and it is *inside* the TreeMap block, so it is TreeMap's own problem.

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
> ancestor chains, so aggregation only happens *above* the leaves. At leaf level the number therefore says
> nothing the cell colour does not already say — it earns its keep only on collapsed elements, where it is
> a sum over their subtrees.

Upstream also drew the weight as a small bar under the number, sized by its decile among all populated
cells. We removed it; see [README.md](README.md) for why it could not work on our data.

## The coloured bars beside the row headers

There are **two** indicator bars per row, and both are **relative to the currently selected row**. They
answer "how does this row relate to the thing I clicked", not "what is this row".

It has to be a **row**: `UpdateRelationFlags` reads `SelectedRow`, and `SelectColumn` sets `SelectedRow` to
null. Clicking a column header therefore draws the crosshair but clears every bar. With nothing selected
there are no bars at all.

### Right bar — at the right edge, against the matrix

`MatrixRowHeaderItemView.GetIndicatorColor()`:

| Colour | Flag | Meaning |
|---|---|---|
| Green | `IsConsumer` | this row **uses** the selected element |
| Blue | `IsProvider` | this row **is used by** the selected element |
| Orange | both | **mutual — a cycle** |

These three are what the legend at the bottom of the tab names (Consumer / Provider / Cyclic). Two other
modes replace this bar entirely: search (`MatrixColorMatch`) and bookmarks (`MatrixColorBookmark`).

Note the axis flip: green here means the row *consumes*, while in the grid a row is the *provider*. The bar
describes a role relative to the selection; the grid describes an axis. The same row can be green, blue or
orange depending on what you select.

### Left bar — at the left edge, this fork's own addition

Appears only when the selected element is **expanded** (`SelectedRow?.Element?.IsExpanded == true`) — that
is, one of the vertical strips down the left side. Every leaf beneath it then gets a bar describing its
relations to the rows *outside* the selection:

| Colour | Flag | Meaning |
|---|---|---|
| Green | `IsConsumerIn` | this leaf depends on something outside the selected subtree |
| Blue | `IsProviderIn` | something outside the selected subtree depends on this leaf |
| Split, blue over green | both | both directions cross the boundary |

The signal is in the **absence**:

> **A leaf with no left bar has no relation crossing the boundary at all — it is used only inside the
> selected subtree.**

Select an expanded assembly and you read off its public surface (blue) versus its internals (no bar) in one
glance.

Note that the fork's own README describes this as "when a collapsed (vertical) element is selected". The
code says `IsExpanded == true`; "vertical" is the giveaway — an element is drawn as a vertical strip
precisely *because* it is expanded and its children occupy the rows.

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
in [README.md](README.md); the short version is that its "Total Cyclicity" counts mutually dependent sibling
pairs over internal relations while ours counts types inside a strongly connected component, and that its
cycle detection cannot see a cycle longer than two.

## Controls

- **Ctrl + mouse wheel** zooms the whole matrix (0.04 – 4.0). Plain wheel scrolls.
- **Click a row header** to select it — this is what drives the indicator bars. Clicking a column header
  selects the column but clears them.
- **Click the arrow** in a row header (the top-left 20×24 px) to expand or collapse; hold **shift** and it
  works recursively.
