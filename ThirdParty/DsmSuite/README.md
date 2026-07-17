# DsmSuite (vendored subset)

The matrix view on the **DSM** tab is DsmSuite's, not ours. This folder holds the part of it we
need, as source, so it can be patched and debugged in one build.

- Origin: <https://github.com/ernstaii/dsmsuite.sourcecode>, commit `d07a26d15d26e177b00649dfa80bfc7e54021ebd`
- Upstream of that fork: <https://github.com/dsmsuite/dsmsuite.sourcecode>
- License: GPL-3.0-or-later (`LICENSE`), originally MIT (`LICENSE-jmuijsenberg`), see
  `ThirdPartyNotices/GPL-3.0-LICENSED-LIBRARIES.txt`

## What was kept

7 of the ~38 projects: `Common.Util`, `Common.Model`, `Analyzer.Model`, `DsmViewer.Model`,
`DsmViewer.Application`, `DsmViewer.ViewModel`, `DsmViewer.View`. The analyzers, the installer, the
`DsmAnalyzer` frontend and all test projects are not here.

`Directory.Build.props` in this folder deliberately does not chain to the repository root one — see
the comment in that file.

## Changes made (GPL §5(a))

Every change is marked in the source with a `Changed 2026-07 for CSharpCodeAnalyst` comment.

| What | Where | Why |
|---|---|---|
| Retargeted `net8.0` / `net8.0-windows` → `net10.0` / `net10.0-windows` | all `.csproj` | match this repository |
| `DsmViewer.View` from `WinExe` to `Library`; removed `App.xaml(.cs)`, `Windows/`, `Properties/PublishProfiles/` | `DsmViewer.View` | it is a hosted control now, not an application |
| `App.Skin` → `ThemeResourceDictionary.Skin` (static on the dictionary) | `Resources/Themes/ThemeResourceDictionary.cs` | the `App` class it read is gone; the host sets the theme |
| Removed the implicit (unkeyed) `TreeViewItem` style | `Resources/Style.xaml` | dead here, but these dictionaries are merged into a host that *does* use TreeViews, and an implicit style would restyle them all |
| Removed `SqlImporter` and its callers (`AsyncImportSqlModel`, `ImportSqlModel`, the `.sql` case) | `Application`, `ViewModel` | the `.sql` import is not offered; drops the Dapper and Microsoft.Data.Sqlite dependencies |
| Added `MainViewModel.ShowInMemoryModel(title)` | `ViewModel/Main/MainViewModel.cs` | show a model built in memory, with no file round trip |
| `DsmElementModel.Clear()` now also clears `_elementsByName` | `Model/Core/DsmElementModel.cs` | **bug fix**, see below |
| Removed the row header and cell context menus | `Matrix/MatrixView.xaml` | their commands edit the DSM model or open the viewer's dialog windows; neither fits a read-only view onto a parsed code graph |
| Removed drag and drop of row headers, and the `IsDropTarget` / `MoveCommand` members behind it | `Matrix/MatrixRowHeaderItemView.cs`, `ViewModel/Matrix/ElementTreeItemViewModel.cs` | dragging a row onto another re-parented the element — the same model editing as the context menus, just without a menu to remove |
| Hid the metrics panel and the button that expands it | `Matrix/MatrixView.xaml`, `Matrix/MatrixTopCornerView.xaml` | its numbers contradict the application's own system metrics — see below |
| Removed the weight bar in the cells, and the decile bucketing behind it (`WeightPercentiles`) | `Matrix/MatrixCellsView.cs`, `ViewModel/Matrix/MatrixViewModel.cs` | the number states the weight already; the bar is degenerate for our data — see below |
| Cell weights are drawn at font size 10 and centred; `DrawText` / `MeasureText` / `CenteredTextBaseline` take an optional font size | `Matrix/MatrixCellsView.cs`, `Matrix/MatrixFrameworkElement.cs` | **bug fix**: four digits did not fit and were silently truncated, see below |
| The infinity sign above 9999 became `>9K` | `Matrix/MatrixCellsView.cs` | it claimed a weight was infinite when it only meant it did not fit; `>9K` states what is known and names the bound |
| Removed the left hand indicator and the flags behind it (`IsConsumerIn` / `IsProviderIn`, `FindLeaves`) | `Matrix/MatrixRowHeaderItemView.cs`, `ViewModel/Matrix/MatrixViewModel.cs`, `ViewModel/Matrix/ElementTreeItemViewModel.cs` | confusing to read, and quadratic per selection — see below |
| Added `MatrixFrameworkElement.Ellipsize`; row and column labels are ellipsized instead of cut, and the row label's width is derived from the order actually drawn | `Matrix/MatrixFrameworkElement.cs`, `Matrix/MatrixRowHeaderItemView.cs`, `Matrix/MatrixColumnHeaderView.cs` | **bug fix**: a long row name ran through the order number, see below |
| Dropped `Weight` and `CycleType` (and the `Legend` list) from the cell tooltip | `ViewModel/Matrix/CellToolTipViewModel.cs`, `ViewModel/Matrix/MatrixViewModel.cs`, `Matrix/MatrixView.xaml` | the cell draws the weight as its number and a cycle as its colour, so the tooltip repeated what the pointer is on; it also spares a `GetDependencyWeight` and an `IsCyclicDependency` per mouse move |
| Added `MatrixViewModel.ColumnElementNames` | `ViewModel/Matrix/MatrixViewModel.cs` | the column headers only had the element order, so every column was a lookup into the row headers |
| Added `MatrixViewModel.LeafAt`, routed the four row/column index lookups through it | `ViewModel/Matrix/MatrixViewModel.cs` | **bug fix**, see below |
| Column headers draw the order right aligned plus the name, anchored at the top of the header | `Matrix/MatrixColumnHeaderView.cs` | show the name, and keep the names aligned across columns although the order is variable width; the anchoring is a **bug fix**, see below |

Not a change to their code, but worth knowing when reading it: everything the matrix draws (colours, cell size, header height) is resolved by key via `FindResource` / `StaticResource`. `Features/DsmMatrix/DsmMatrixTheme.xaml` overrides those keys from our side, merged last in `App.xaml`. Restyling therefore needs no edit in here — prefer that route.

## Why the metrics panel is hidden

DsmSuite's metrics column answers questions that sound like the ones our own `SystemMetricsAnalysis`
answers, with different definitions and the same unit. For "Total Cyclicity" on this repository it showed
**0.489 %** against our **1.4 %**:

| | CSharpCodeAnalyst | DsmSuite |
|---|---|---|
| Question | what share of **types** is entangled? | what share of **relations** is a mutual pair? |
| Numerator | types in an SCC of size ≥ 2 (Tarjan) | count of mutually dependent **sibling pairs** |
| Denominator | all types | internal relations |
| Cycle length seen | any | **2 only** |

Not the same quantity computed differently — a different numerator over a different denominator, both
printed as a percentage.

The last row is the important one. `DsmRelationModel.IsCyclicDependency` only ever checks `a -> b` and
`b -> a`, and `CountCycles` only ever compares siblings under a common parent. **A cycle of three or more
is invisible to it**, so its cyclicity is not a weaker estimate of ours, it is blind to most of what we
report. Two contradicting answers to "how cyclic is this" in one application is not something a user can
resolve, so the panel is hidden rather than explained.

The panel is collapsed, not deleted; bringing back a single metric (element counts, ingoing/outgoing
relations — the ones that do not clash) is a one line change in `MatrixView.xaml`.

## Why the weight bar is gone

Each populated cell used to carry a small dark bar under the number, its width the weight's decile among
all populated cells. It was removed because for our data it cannot say anything:

- **Fully expanded, every weight is 1.** `TypeGraph` deduplicates, so there is exactly one edge per pair of
  types, and the builder writes it with `weight: 1`. `DsmRelationModel.AddWeights` sums a relation's weight
  along both ancestor chains, so aggregation only happens above the leaves. At leaf level the cell is
  therefore 0 or 1 — which the cell colour already tells you — and every bar came out the same length.
- **Under ten populated cells the deciles collapse.** `stepSize = sortedWeights.Count / 10` is 0, every
  bucket lands on the smallest weight, and every populated cell draws a 90 % bar.

The bucketing that fed it (`_weightPercentiles`, `_nrWeightBuckets`) went with it rather than staying as
dead work: it allocated a `double` per cell, i.e. `matrixSize²`, on a view where `matrixSize` is already
the thing that hurts.

The numbers still carry information **above** the leaves, where they are sums over the subtrees.

## Why the left hand indicator is gone

This fork added a second bar to the row headers, at the left edge. Selecting an *expanded* element marked
every leaf beneath it that had a relation reaching outside the selection: green for consuming outwards,
blue for being consumed from outside, split for both. Its actual signal was the **absence** of a bar — a
leaf without one is used only inside the subtree.

It reads badly. It uses the same two colours as the right hand indicator with a different meaning, sits on
the opposite edge of the same row, and the one thing it is good at only registers if you notice something
that is not drawn. Two bars in the same colours saying different things, one of which speaks by not being
there, is more puzzle than help.

It was also expensive: `UpdateRelationFlags` ran `GetDependencyWeight` for every leaf of the selection
against every leaf outside it, on every selection. One click on a row header of a fully expanded tree cost
a quadratic sweep. The flags (`IsConsumerIn` / `IsProviderIn`) and `FindLeaves` had no other reader, so they
went too.

The right hand indicator — consumer / provider / cyclic, the one the legend explains — is untouched.

## Bugs found in the vendored code

Neither is fixed beyond what we needed; both are worth reporting upstream.

1. **`DsmElementModel.Clear()` did not clear `_elementsByName`.** `AddElement` resolves through
   `FindElementByFullname`, so after a `Clear` it returned stale elements from the previous
   population — no longer registered by id, no longer attached to the root. Upstream never hits this
   because every import runs against a freshly constructed model. **Fixed here**, since our builder
   calls `Clear()`.

2. **`MatrixColumnHeaderView` anchored header text at the bottom of the header.** The draw origin was
   `MatrixHeaderHeight - 10 - MeasureText(content)`, so the label grew upwards and one as wide as the
   header started above `y = 0` and lost its leading characters — while `DrawText` clipped the tail at
   `maxWidth` at the same time. Text was cut off at *both* ends. Invisible upstream, where the header only
   ever held a short number that always fit. **Fixed here** by anchoring at the top, which our headers need
   because they carry names; it also lines the element orders up across columns.

3. **`MatrixViewModel` indexed its leaves with an unbounded index from the mouse position.** The views turn
   a mouse position into a row/column by plain division and never bound it against the matrix
   (`MatrixCellsView.GetHoveredRow`), so every consumer has to cope with an out of range index.
   `GetRowCoord` / `GetColumnCoord` did check — but only against the upper bound, never against a negative
   — while `UpdateCellTooltip` and `UpdateColumnHeaderTooltip` checked only `HasValue` and threw an
   `ArgumentOutOfRangeException`. Reachable upstream too (hover a cell, then shrink the matrix under the
   pointer with the toolbar zoom), just far easier to hit with a wheel zoom, where the pointer is over the
   cells by definition. **Fixed here** via `LeafAt`, which all four now share; the two tooltip methods
   already null-checked their result, so the null it returns is enough.

4. **A cell weight of four digits was silently drawn as a different number.** `DrawText` tests the running
   width *before* each glyph, so a glyph is kept whenever the text so far is still under `maxWidth` — and
   dropped without a trace once it is not. A cell leaves 22px, a digit at the shared font size of 14 is
   7.55px, so the fourth digit always fell off the end: **1000 was drawn as `100`, 9999 as `999`**. Not an
   overflow, not an exception — a wrong number. The infinity sign above 9999 shows the author knew about
   the width bound but put it an order of magnitude too high; three digits is the real limit. Also why
   `455` looked like it leaked into its neighbour: at 22.64px it does fit, with 0.7px to spare on each
   side. **Fixed here** by drawing the cells at font size 10, where four digits take 21.6px and three get
   3.9px of air.

5. **A long row header name was drawn straight through the element order.** The label got a fixed budget of
   `ActualWidth - 70`, which reserves room for a *three digit* order — the same three digit assumption as
   bug 4, in a second place. Order counts up to the number of elements in the whole tree, so four digits are
   the normal case, and the name then overlapped the number. It only shows on an indented row, where
   `ActualWidth` is the full column minus the indent: at 344px with order `1010` the overlap is 7.1px, at
   the full 400px there is none. **Fixed here** by deriving the budget from the order that is actually
   drawn (`OrderLeftEdge`), with a gap.

   Related, and fixed with it: neither the row nor the column labels had an ellipsis. `DrawText` just stops
   emitting glyphs, so `CodeElementFactory` and `CodeElementFilter` both end up reading `CodeElement` with
   nothing to say a cut happened. `Ellipsize` makes it visible.

6. **`DsmApplication.LoadModel` does not rebind `DsmQueries`.** `_queries` is readonly and bound to
   the model passed to the constructor, but `LoadModel` swaps `_dsmModel` underneath it. After
   opening a file, every query routed through `_queries` (the "list consumers/providers" commands)
   runs against the *initial* model. **Not fixed** — we avoid it instead by populating the model
   before constructing `DsmApplication`, so no swap ever happens. Do not introduce a `LoadModel` call
   without fixing this first.
