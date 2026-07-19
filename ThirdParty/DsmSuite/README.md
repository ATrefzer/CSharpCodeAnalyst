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

`Directory.Build.props` in this folder deliberately does NOT import the repository root Directory.Build.props— see
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
| Removed the weight bar in the cells, and the decile bucketing behind it (`WeightPercentiles`) | `Matrix/MatrixCellsView.cs`, `ViewModel/Matrix/MatrixViewModel.cs` | the number states the weight already. Visual clutter that consumes space in the cell. |
| Cell weights are drawn at font size 10 and centred; `DrawText` / `MeasureText` / `CenteredTextBaseline` take an optional font size | `Matrix/MatrixCellsView.cs`, `Matrix/MatrixFrameworkElement.cs` | **bug fix**: four digits did not fit and were silently truncated, see below |
| The infinity sign above 9999 became `>9K` | `Matrix/MatrixCellsView.cs` | it claimed a weight was infinite when it only meant it did not fit; `>9K` states what is known and names the bound |
| Removed the left hand indicator and the flags behind it (`IsConsumerIn` / `IsProviderIn`, `FindLeaves`) | `Matrix/MatrixRowHeaderItemView.cs`, `ViewModel/Matrix/MatrixViewModel.cs`, `ViewModel/Matrix/ElementTreeItemViewModel.cs` | Confusing to read, and quadratic per selection. Right hand indicator is untouched. |
| Added `MatrixFrameworkElement.Ellipsize`; row and column labels are ellipsized instead of cut, and the row label's width is derived from the order actually drawn | `Matrix/MatrixFrameworkElement.cs`, `Matrix/MatrixRowHeaderItemView.cs`, `Matrix/MatrixColumnHeaderView.cs` | **bug fix**: a long row name ran through the order number, see below |
| Dropped `CycleType` (and the `Legend` list) from the cell tooltip | `ViewModel/Matrix/CellToolTipViewModel.cs`, `ViewModel/Matrix/MatrixViewModel.cs`, `Matrix/MatrixView.xaml` | a cycle is the cell's colour, so the entry repeated what the pointer is on; it also spares an `IsCyclicDependency` per mouse move. `Weight` went too and came back — see the next row |
| Below zoom 0.7 the cell weights are not drawn and a populated cell is filled near-black instead; added `GetPresenceBackground` and four brush slots for it; `ZoomLevel` triggers a redraw, but only when it crosses that threshold | `Matrix/MatrixCellsView.cs`, `Matrix/MatrixTheme.cs` | the number scales with the zoom, so it never stops fitting — it just stops being legible, and an illegible glyph only tints its cell, which hides the one thing worth seeing zoomed out: where the dependencies are. Redrawing on every zoom step instead froze the application: `OnRender` walks `matrixSize²` cells and one wheel spin is a dozen steps. See `Dsm.md` |
| The hover / selection crosshair darkens the cell by a fixed number of channel steps instead of multiplying it; `HighlightFactorHovered` / `HighlightFactorSelected` now carry those steps (26 / 45), and the combined variant adds them | `Matrix/MatrixTheme.cs`, and our `Features/DsmMatrix/DsmMatrixTheme.xaml` | **bug fix**, see below |
| Brushes derived at runtime are frozen | `Matrix/MatrixTheme.cs` | **bug fix**, see below |
| Dropped the element ids from both tooltips (`ConsumerId` / `ProviderId`, `Id`); kept `Weight` in the cell tooltip; in both tooltip grids the label column is `Auto` instead of a fixed 100 and the value column is star instead of `Auto` | `ViewModel/Matrix/CellToolTipViewModel.cs`, `ViewModel/Matrix/ElementToolTipViewModel.cs`, `ViewModel/Matrix/MatrixViewModel.cs`, `Matrix/MatrixView.xaml` | the ids are DsmSuite's own numbering and identify nothing outside the DSM model. The weight has to stay because the cell stops drawing it below zoom 0.7, which is where the tooltip becomes the only way to tell a populated cell from an empty one. On the layout: the fixed 100 was wider than any remaining label, and the title spans the columns and is wider than any data row — WPF hands that surplus to the `Auto` columns inside the span, which pushed the values away from their colons. A star column absorbs it instead |
| Shift + wheel scrolls horizontally | `Matrix/MatrixView.xaml.cs` | a ScrollViewer only handles the wheel vertically, so the horizontal bar was the only way across — and the bars are inside the scaled grid, so zooming out makes them too thin to grab. See below. |
| Added `MatrixViewModel.ColumnElementNames` | `ViewModel/Matrix/MatrixViewModel.cs` | the column headers only had the element order, so every column was a lookup into the row headers |
| Added `MatrixViewModel.LeafAt`, routed the four row/column index lookups through it | `ViewModel/Matrix/MatrixViewModel.cs` | **bug fix**, see below |
| Column headers draw the order right aligned plus the name, anchored at the top of the header | `Matrix/MatrixColumnHeaderView.cs` | show the name, and keep the names aligned across columns although the order is variable width; the anchoring is a **bug fix**, see below |
| Every `OnDataContextChanged` unsubscribes from the previous view model; added `MatrixRowHeaderItemView.Detach`, called from `MatrixRowHeaderView.CreateChildViews`; the three `OnPropertyChanged` handlers return early on a null view model | `Matrix/MatrixCellsView.cs`, `Matrix/MatrixColumnHeaderView.cs`, `Matrix/MatrixRowMetricsView.cs`, `Matrix/MatrixRowHeaderView.cs`, `Matrix/MatrixRowHeaderItemView.cs` | **bug fix**: discarded views stayed subscribed, so hovering threw a `NullReferenceException` and the row header items leaked, see below |
| Removed `<GitRemote>github</GitRemote>` | `DsmSuite.Common.Util/DsmSuite.Common.Util.csproj` | upstream read the repository url from a remote named `github`; a normal clone of this fork has none, so GitInfo emitted `warning GI002: Could not retrieve repository url for remote 'github'` on every build — and on every solution import, where `MSBuildWorkspace` surfaces it as a diagnostic in our own error dialog. GitInfo defaults the property to `origin`, so removing it is the fix; `SystemInfo.Version` now reports a real commit instead of an empty one |

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



The last row is the important one. `DsmRelationModel.IsCyclicDependency` only ever checks `a -> b` and
`b -> a`, and `CountCycles` only ever compares siblings under a common parent. **A cycle of three or more
is invisible to it**, so its cyclicity is not a weaker estimate of ours, it is blind to most of what we
report. Two contradicting answers to "how cyclic is this" in one application is not something a user can
resolve, so the panel is hidden rather than explained.

The panel is collapsed, not deleted; bringing back a single metric (element counts, ingoing/outgoing
relations — the ones that do not clash) is a one line change in `MatrixView.xaml`.

## The scroll bars scale with the zoom

`MatrixView`'s `ScaleTransform` sits on the outer grid, and the `ScrollViewer` is inside it, so zooming out
shrinks the scroll bars along with the matrix — at 0.04 the bar is well under a pixel wide.

Moving the `ScrollViewer` out of the transform is not the small fix it looks like: the row and column headers
have to scale *with* the cells or the row heights stop lining up, the 400px header column and the header
height scale with them today, and the scroll synchronisation (`Canvas.SetLeft(ColumnHeaderView,
-e.HorizontalOffset)`) works in the scaled coordinate space.

Instead the wheel was made sufficient — plain for vertical (upstream already forwards it from anywhere in
the matrix), shift for horizontal (added). The wheel moves by a fixed number of content units, so it covers
the same couple of cells at any zoom. The thin bar stays as a cosmetic wart.

If it ever needs fixing properly: a counter-scaling `LayoutTransform` on the scroll bars, from an implicit
`Style TargetType="ScrollBar"` inside `<ScrollViewer.Resources>` — scoped there, so it would not leak into
the host application the way an unkeyed style at application scope would.

## Bugs found in the vendored code

Each says whether it is fixed here. Nothing is fixed beyond what we needed — several of these are
unreachable upstream, where the viewer owns its own window, keeps one matrix for the lifetime of the
process, and ships palettes that happen to sidestep them.

1. **`DsmElementModel.Clear()` did not clear `_elementsByName`.** `AddElement` resolves through
   `FindElementByFullname`, so after a `Clear` it returned stale elements from the previous
   population. Upstream never hits this
   because every import runs against a freshly constructed model. **Fixed here**, since our builder
   calls `Clear()`.

2. **`MatrixColumnHeaderView` anchored header text at the bottom of the header.** The draw origin was
   `MatrixHeaderHeight - 10 - MeasureText(content)`, so the label grew upwards and one as wide as the
   header started above `y = 0` and lost its leading characters — while `DrawText` clipped the tail at
   `maxWidth` at the same time. Text was cut off at *both* ends. Invisible upstream, where the header only
   ever held a short number that always fit. **Fixed here** by anchoring at the top.
   
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

6. **The matrix views subscribe to their view model's `PropertyChanged` and never unsubscribe.** Every view
   under `Matrix/` attaches in `OnDataContextChanged` (`MatrixRowHeaderItemView` in its constructor) and
   has no matching detach. Two consequences, both real here:

   *It crashes.* Our matrix sits in a `TabControl`, which keeps one content presenter and rebuilds the
   tab's visual tree on every switch, while the view model lives on the tab's own view model and survives.
   Switching away and back therefore leaves the discarded view subscribed to the live view model — and
   reachable from it, so it is not collected. Its `DataContext` is gone by then, so its `_viewModel` is
   null, and `OnPropertyChanged` is the one member of these classes that dereferences it without a guard
   (`OnRender` and the mouse handlers all check). The next hover raises `CellToolTipViewModel`, the stale
   handler runs, `NullReferenceException`. Same defect in `MatrixColumnHeaderView` via the column header
   tooltip, and in `MatrixRowMetricsView`, which only escapes it because the metrics panel is collapsed.
   Zooming out makes it near certain rather than occasional: cells are a few pixels wide, so one mouse
   move crosses many of them and re-raises the tooltip almost continuously.

   *It leaks.* `MatrixRowHeaderView.CreateChildViews` builds a fresh `MatrixRowHeaderItemView` per row on
   every size change and every tree change, and each one had subscribed to the long lived matrix view
   model in its constructor. All generations stayed alive and kept redrawing themselves on hover.

   Invisible upstream: the viewer owns a single window that holds one matrix for the lifetime of the
   process, so no view is ever discarded while its view model lives on. **Fixed here** — every
   `OnDataContextChanged` detaches from `e.OldValue` first, `MatrixRowHeaderItemView` gained a `Detach`
   that `CreateChildViews` calls before it clears its children, and the three `OnPropertyChanged` handlers
   return early on a null view model, matching what the rest of each class already assumes.

7. **Brushes derived at runtime were never frozen.** `GetHighlightBrush` builds the hover and selected
   variants with `new SolidColorBrush(...)` and puts them straight into the cache. Every use of a *mutable*
   `Freezable` in a `DrawingContext` costs WPF a change subscription, and `MatrixCellsView.OnRender` issues
   one `DrawRectangle` per cell — so a brush that lands on the bulk of the matrix is used `matrixSize²`
   times. The brushes that come from the resource dictionaries are all declared `po:Freeze="True"`, so the
   omission is in the derived ones only, and those normally reach just the hovered and selected row and
   column — `2 × matrixSize` cells. It surfaced while a weakened depth ramp was being tried for small zoom
   levels: that gave *every* cell a derived brush, and the application stopped responding — not slowly, but
   in the way hundreds of thousands of change subscriptions stop an application. **Fixed here**: everything
   derived goes through `Frozen` before it is cached.

   Note the ramp was then dropped for unrelated, visual reasons, so freezing is latent again rather than
   load-bearing. It stays because it is free and because it turns "derive a brush that covers many cells"
   from a trap into an ordinary thing to do.

8. **The crosshair was a multiplication, so its strength depended on the cell it crossed.**
   `Color.Multiply(colour, 1.1)` moves a colour proportionally to how bright it already is. On the deepest
   step of our depth ramp (`#748C9E`) the hover moved the channels by 11 to 15, which is barely visible;
   on the empty cell (`#E4E7EA`), which is most of the matrix, green and blue hit the 255 ceiling
   immediately, so the cell did not get brighter — it got *warmer*, a hue shift where contrast was
   intended. Raising the factor makes both worse: it clips the light end sooner while still under-moving
   the dark one. Upstream is exposed to this too but hides it, because its own palettes sit in a narrow
   band of lightness where a proportional step happens to be roughly uniform. **Fixed here** by darkening
   a fixed number of channel steps instead. It also removed a constraint the palette had been living
   under: colours no longer have to stay below roughly 210 to keep reacting to hover.

9. **`DsmApplication.LoadModel` does not rebind `DsmQueries`.** `_queries` is readonly and bound to
   the model passed to the constructor, but `LoadModel` swaps `_dsmModel` underneath it. After
   opening a file, every query routed through `_queries` (the "list consumers/providers" commands)
   runs against the *initial* model. **Not fixed** — we avoid it instead by populating the model
   before constructing `DsmApplication`, so no swap ever happens. Do not introduce a `LoadModel` call
   without fixing this first.
