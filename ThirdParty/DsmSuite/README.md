# DsmSuite (vendored subset)

The matrix view on the **DSM** tab is DsmSuite's, not ours. This folder holds the part of it we
need, as source, so it can be patched and debugged in one build.

- Origin: <https://github.com/ernstaii/dsmsuite.sourcecode>, commit `8a41375d6bb49b0e5f6b5ee1bcec61eb966d0f5a`
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

Every change we still carry is marked in the source with a `Changed 2026-07 for CSharpCodeAnalyst`
comment. Four bug fixes originally made here were contributed back and merged upstream (into the pinned
commit above), so they are no longer listed below: `DsmElementModel.Clear()` clearing `_elementsByName`,
the `LeafAt` bounds check, and the view-model `PropertyChanged` unsubscribe are now part of the baseline
and unmarked in the source; freezing the derived brushes went upstream too, but our copy still applies it
inside the un-upstreamed crosshair rewrite, so it stays noted on that change-table row. Upstream has since
advanced further (including matrix-draw performance work) that we have not vendored.

| What | Where | Why |
|---|---|---|
| Retargeted `net8.0` / `net8.0-windows` → `net10.0` / `net10.0-windows` | all `.csproj` | match this repository |
| `DsmViewer.View` from `WinExe` to `Library`; removed `App.xaml(.cs)`, `Windows/`, `Properties/PublishProfiles/` | `DsmViewer.View` | it is a hosted control now, not an application |
| `App.Skin` → `ThemeResourceDictionary.Skin` (static on the dictionary) | `Resources/Themes/ThemeResourceDictionary.cs` | the `App` class it read is gone; the host sets the theme |
| Removed the implicit (unkeyed) `TreeViewItem` style | `Resources/Style.xaml` | dead here, but these dictionaries are merged into a host that *does* use TreeViews, and an implicit style would restyle them all |
| Removed `SqlImporter` and its callers (`AsyncImportSqlModel`, `ImportSqlModel`, the `.sql` case) | `Application`, `ViewModel` | the `.sql` import is not offered; drops the Dapper and Microsoft.Data.Sqlite dependencies |
| Added `MainViewModel.ShowInMemoryModel(title)` | `ViewModel/Main/MainViewModel.cs` | show a model built in memory, with no file round trip |
| Removed the row header and cell context menus | `Matrix/MatrixView.xaml` | their commands edit the DSM model or open the viewer's dialog windows; neither fits a read-only view onto a parsed code graph |
| Restored one cell context menu item, "Show relation matrix" (`ShowCellDetailMatrixCommand`); both `ShowCellDetailMatrixExecute` and `HomeExecute` also reset the zoom to 1.0 | `Matrix/MatrixView.xaml`, `ViewModel/Main/MainViewModel.cs`, and our `Features/DsmMatrix/DsmMatrixView.xaml` | it drills the matrix down to the clicked cell's provider / consumer subtrees by filtering `IsIncludedInTree` (a read-only view filter via `ShowElementDetail`, not a graph edit), so unlike the rest of that menu it fits the read-only view. `Reload` keeps the zoom, so without the reset a drill from a zoomed-out overview showed the small detail matrix at that tiny scale. A Home button on our toolbar (`Matrix.HomeCommand`) restores the full matrix and resets the zoom |
| Removed drag and drop of row headers, and the `IsDropTarget` / `MoveCommand` members behind it | `Matrix/MatrixRowHeaderItemView.cs`, `ViewModel/Matrix/ElementTreeItemViewModel.cs` | dragging a row onto another re-parented the element — the same model editing as the context menus, just without a menu to remove |
| Hid the metrics panel and the button that expands it | `Matrix/MatrixView.xaml`, `Matrix/MatrixTopCornerView.xaml` | its numbers contradict the application's own system metrics — see below |
| Collapsed the top-left corner's clear-selection button | `Matrix/MatrixTopCornerView.xaml` | it is a contentless, transparent 50x50 button — an invisible click target — and being `Visible` it reserved ~60px in the header row. That floored the row height, so once the column-name toggle collapsed the header below it the gap under the header jumped. `Collapsed` removes the hotspot and the floor; clicking another element still clears the selection |
| Removed the weight bar in the cells, and the decile bucketing behind it (`WeightPercentiles`) | `Matrix/MatrixCellsView.cs`, `ViewModel/Matrix/MatrixViewModel.cs` | the number states the weight already. Visual clutter that consumes space in the cell. |
| Cell weights are drawn at font size 10 and centred; `DrawText` / `MeasureText` / `CenteredTextBaseline` take an optional font size | `Matrix/MatrixCellsView.cs`, `Matrix/MatrixFrameworkElement.cs` | **bug fix**: four digits did not fit and were silently truncated, see below |
| The infinity sign above 9999 became `>9K` | `Matrix/MatrixCellsView.cs` | it claimed a weight was infinite when it only meant it did not fit; `>9K` states what is known and names the bound |
| Removed the left hand indicator and the flags behind it (`IsConsumerIn` / `IsProviderIn`, `FindLeaves`) | `Matrix/MatrixRowHeaderItemView.cs`, `ViewModel/Matrix/MatrixViewModel.cs`, `ViewModel/Matrix/ElementTreeItemViewModel.cs` | Confusing to read, and quadratic per selection. Right hand indicator is untouched. |
| Added `MatrixFrameworkElement.Ellipsize`; row and column labels are ellipsized instead of cut, and the row label's width is derived from the order actually drawn | `Matrix/MatrixFrameworkElement.cs`, `Matrix/MatrixRowHeaderItemView.cs`, `Matrix/MatrixColumnHeaderView.cs` | **bug fix**: a long row name ran through the order number, see below |
| Dropped `CycleType` (and the `Legend` list) from the cell tooltip | `ViewModel/Matrix/CellToolTipViewModel.cs`, `ViewModel/Matrix/MatrixViewModel.cs`, `Matrix/MatrixView.xaml` | a cycle is the cell's colour, so the entry repeated what the pointer is on; it also spares an `IsCyclicDependency` per mouse move. `Weight` went too and came back — see the next row |
| Below zoom 0.7 the cell weights are not drawn and a populated cell is filled near-black instead; added `GetPresenceBackground` and four brush slots for it; `ZoomLevel` triggers a redraw, but only when it crosses that threshold | `Matrix/MatrixCellsView.cs`, `Matrix/MatrixTheme.cs` | the number scales with the zoom, so it never stops fitting — it just stops being legible, and an illegible glyph only tints its cell, which hides the one thing worth seeing zoomed out: where the dependencies are. Redrawing on every zoom step instead froze the application: `OnRender` walks `matrixSize²` cells and one wheel spin is a dozen steps. See `Dsm.md` |
| The hover / selection crosshair darkens the cell by a fixed number of channel steps instead of multiplying it; `HighlightFactorHovered` / `HighlightFactorSelected` now carry those steps (26 / 45), and the combined variant adds them; the derived brushes are frozen (matching upstream), extracted into a `Frozen` helper since the rewrite derives them in more than one place | `Matrix/MatrixTheme.cs`, and our `Features/DsmMatrix/DsmMatrixTheme.xaml` | **bug fix**, see below |
| Dropped the element ids from both tooltips (`ConsumerId` / `ProviderId`, `Id`); kept `Weight` in the cell tooltip; in both tooltip grids the label column is `Auto` instead of a fixed 100 and the value column is star instead of `Auto` | `ViewModel/Matrix/CellToolTipViewModel.cs`, `ViewModel/Matrix/ElementToolTipViewModel.cs`, `ViewModel/Matrix/MatrixViewModel.cs`, `Matrix/MatrixView.xaml` | the ids are DsmSuite's own numbering and identify nothing outside the DSM model. The weight has to stay because the cell stops drawing it below zoom 0.7, which is where the tooltip becomes the only way to tell a populated cell from an empty one. On the layout: the fixed 100 was wider than any remaining label, and the title spans the columns and is wider than any data row — WPF hands that surplus to the `Auto` columns inside the span, which pushed the values away from their colons. A star column absorbs it instead |
| Shift + wheel scrolls horizontally; both axes are handled explicitly and one notch scrolls a fixed fraction of the viewport | `Matrix/MatrixView.xaml.cs` | a ScrollViewer only handles the wheel vertically, so the horizontal bar was the only way across — and the bars are inside the scaled grid, so zooming out makes them too thin to grab. The scroll offset is in pre-zoom coordinates and the viewport shrinks as the zoom grows, so a fixed content step moved almost nothing on screen when zoomed out (a couple of rows out of hundreds) and a screenful when zoomed in; a fraction of the viewport is the same fraction of the screen at any zoom. See below. |
| Added `MatrixViewModel.ColumnElementNames` | `ViewModel/Matrix/MatrixViewModel.cs` | the column headers only had the element order, so every column was a lookup into the row headers |
| Column headers draw the order right aligned plus the name, anchored at the top of the header | `Matrix/MatrixColumnHeaderView.cs` | show the name, and keep the names aligned across columns although the order is variable width; the anchoring is a **bug fix**, see below |
| Added `MatrixViewModel.ColumnNamesVisible`; when it is off the column header draws the element order alone and shrinks to fit just that number (reserving room for at least a four digit order), and the hosting `Canvas` binds its `Height` to the header view so the whole header strip collapses | `ViewModel/Matrix/MatrixViewModel.cs`, `Matrix/MatrixColumnHeaderView.cs`, `Matrix/MatrixView.xaml` | a toolbar toggle (hosted on our side in `Features/DsmMatrix/DsmMatrixView.xaml`, above the zoomed matrix) reclaims the tall header band when the names are not needed |
| The hover / selection crosshair is a translucent overlay (`MatrixCrosshairView`, new) drawn over the cells instead of into each cell; `MatrixCellsView` no longer takes hover / selection into its cell colours and no longer invalidates on them; `MatrixCellsView.HitTestCore` is a bounds test; neither the column nor the row header highlights (or invalidates on) the hovered column / row anymore | `Matrix/MatrixCrosshairView.cs` (new), `Matrix/MatrixCellsView.cs`, `Matrix/MatrixColumnHeaderView.cs`, `Matrix/MatrixRowHeaderItemView.cs`, `Matrix/MatrixView.xaml` | **performance**: with the highlight baked into every cell, one mouse move re-rendered the whole matrix and the composition thread re-rasterised it at the current zoom — a dotTrace timeline of moving the mouse at high zoom was 57 % `MilComposition_SyncFlush`, while the cells' own `OnRender` was under 3 %. The overlay is four fixed rectangles moved by a `TranslateTransform` on a hover change (no re-render, and only the band region is re-composited — an earlier version that redrew a matrix-sized overlay in `OnRender` per hover still showed 61 % `MilComposition_SyncFlush`, because re-compositing that translucent full-size layer is itself the cost); the bounds hit test replaces a geometry walk over `matrixSize²` rectangles per move (`MilUtility_PolygonHitTest`); and neither header re-rasterises on hover anymore (the column header redrew `matrixSize` rotated glyph runs, the row header every one of its item views, on each hovered column / row change). Both headers still highlight the selected row / column |
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
process, and ships palettes that happen to sidestep them. (Four more — a `Clear()` that missed
`_elementsByName`, an unbounded leaf index, views that never unsubscribed, and unfrozen derived brushes —
were contributed back and merged upstream, so they are part of the pinned commit rather than listed here.)

1. **`MatrixColumnHeaderView` anchored header text at the bottom of the header.** The draw origin was
   `MatrixHeaderHeight - 10 - MeasureText(content)`, so the label grew upwards and one as wide as the
   header started above `y = 0` and lost its leading characters — while `DrawText` clipped the tail at
   `maxWidth` at the same time. Text was cut off at *both* ends. Invisible upstream, where the header only
   ever held a short number that always fit. **Fixed here** by anchoring at the top.
   
2. **A cell weight of four digits was silently drawn as a different number.** `DrawText` tests the running
   width *before* each glyph, so a glyph is kept whenever the text so far is still under `maxWidth` — and
   dropped without a trace once it is not. A cell leaves 22px, a digit at the shared font size of 14 is
   7.55px, so the fourth digit always fell off the end: **1000 was drawn as `100`, 9999 as `999`**. Not an
   overflow, not an exception — a wrong number. The infinity sign above 9999 shows the author knew about
   the width bound but put it an order of magnitude too high; three digits is the real limit. Also why
   `455` looked like it leaked into its neighbour: at 22.64px it does fit, with 0.7px to spare on each
   side. **Fixed here** by drawing the cells at font size 10, where four digits take 21.6px and three get
   3.9px of air.

3. **A long row header name was drawn straight through the element order.** The label got a fixed budget of
   `ActualWidth - 70`, which reserves room for a *three digit* order — the same three digit assumption as
   bug 2, in a second place. Order counts up to the number of elements in the whole tree, so four digits are
   the normal case, and the name then overlapped the number. It only shows on an indented row, where
   `ActualWidth` is the full column minus the indent: at 344px with order `1010` the overlap is 7.1px, at
   the full 400px there is none. **Fixed here** by deriving the budget from the order that is actually
   drawn (`OrderLeftEdge`), with a gap.

   Related, and fixed with it: neither the row nor the column labels had an ellipsis. `DrawText` just stops
   emitting glyphs, so `CodeElementFactory` and `CodeElementFilter` both end up reading `CodeElement` with
   nothing to say a cut happened. `Ellipsize` makes it visible.

4. **The crosshair was a multiplication, so its strength depended on the cell it crossed.**
   `Color.Multiply(colour, 1.1)` moves a colour proportionally to how bright it already is. On the deepest
   step of our depth ramp (`#748C9E`) the hover moved the channels by 11 to 15, which is barely visible;
   on the empty cell (`#E4E7EA`), which is most of the matrix, green and blue hit the 255 ceiling
   immediately, so the cell did not get brighter — it got *warmer*, a hue shift where contrast was
   intended. Raising the factor makes both worse: it clips the light end sooner while still under-moving
   the dark one. Upstream is exposed to this too but hides it, because its own palettes sit in a narrow
   band of lightness where a proportional step happens to be roughly uniform. **Fixed here** by darkening
   a fixed number of channel steps instead. It also removed a constraint the palette had been living
   under: colours no longer have to stay below roughly 210 to keep reacting to hover.

5. **`DsmApplication.LoadModel` does not rebind `DsmQueries`.** `_queries` is readonly and bound to
   the model passed to the constructor, but `LoadModel` swaps `_dsmModel` underneath it. After
   opening a file, every query routed through `_queries` (the "list consumers/providers" commands)
   runs against the *initial* model. **Not fixed** — we avoid it instead by populating the model
   before constructing `DsmApplication`, so no swap ever happens. Do not introduce a `LoadModel` call
   without fixing this first.
