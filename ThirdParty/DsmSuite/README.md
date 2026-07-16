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

## Bugs found in the vendored code

Neither is fixed beyond what we needed; both are worth reporting upstream.

1. **`DsmElementModel.Clear()` did not clear `_elementsByName`.** `AddElement` resolves through
   `FindElementByFullname`, so after a `Clear` it returned stale elements from the previous
   population — no longer registered by id, no longer attached to the root. Upstream never hits this
   because every import runs against a freshly constructed model. **Fixed here**, since our builder
   calls `Clear()`.

2. **`DsmApplication.LoadModel` does not rebind `DsmQueries`.** `_queries` is readonly and bound to
   the model passed to the constructor, but `LoadModel` swaps `_dsmModel` underneath it. After
   opening a file, every query routed through `_queries` (the "list consumers/providers" commands)
   runs against the *initial* model. **Not fixed** — we avoid it instead by populating the model
   before constructing `DsmApplication`, so no swap ever happens. Do not introduce a `LoadModel` call
   without fixing this first.
