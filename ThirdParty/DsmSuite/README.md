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
| Hid the metrics panel and the button that expands it | `Matrix/MatrixView.xaml`, `Matrix/MatrixTopCornerView.xaml` | its numbers contradict the application's own system metrics — see below |
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

4. **`DsmApplication.LoadModel` does not rebind `DsmQueries`.** `_queries` is readonly and bound to
   the model passed to the constructor, but `LoadModel` swaps `_dsmModel` underneath it. After
   opening a file, every query routed through `_queries` (the "list consumers/providers" commands)
   runs against the *initial* model. **Not fixed** — we avoid it instead by populating the model
   before constructing `DsmApplication`, so no swap ever happens. Do not introduce a `LoadModel` call
   without fixing this first.
