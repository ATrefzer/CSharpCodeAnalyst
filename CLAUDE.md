# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Platform & Runtime

- Windows-only WPF application (`CSharpCodeAnalyst.csproj` targets `net10.0-windows`, `OutputType=WinExe`). Building / running on Linux/macOS is not supported.
- Requires **.NET 10 SDK** (build) and the **.NET 10 Runtime** plus **MSBuild** from a Visual Studio or .NET SDK install (the parser loads solutions through `MSBuildWorkspace`).
- Line endings: the repo is full of Windows-native WPF assets; do not reformat or convert CRLFs.

## Common commands

Run from the repository root.

```bash
# Restore and build the whole solution
dotnet restore
dotnet build

# Release build with explicit version (matches CI)
dotnet build --no-restore -c Release -p:Version=0.9.0 -p:FileVersion=0.9.0 -p:AssemblyVersion=0.9.0

# Run all tests
dotnet test --configuration Release

# Run a single NUnit fixture or test (filter by fully-qualified name)
dotnet test Tests/Tests.csproj --filter "FullyQualifiedName~CyclesApprovalTests"
dotnet test Tests/Tests.csproj --filter "FullyQualifiedName=CodeParserTests.ApprovalTests.CyclesApprovalTests.MethodName"

# Publish the WPF app (framework-dependent, win-x64, same flags as the release pipeline)
dotnet publish .\CSharpCodeAnalyst\CSharpCodeAnalyst.csproj -c Release -r win-x64 -o publish `
  --self-contained false -p:PublishSingleFile=false -p:SatelliteResourceLanguages=en
```

Command-line (headless) validation mode — triggered when more than one CLI arg is passed, so `App.OnStartup` skips the UI and exits with a status code:

```
CSharpCodeAnalyst.exe -validate -sln:<path.sln> -rules:<path.txt> [-log-console] [-log-file:<file>] [-out:<file>]
```

Exit codes: `0` = clean, `1` = violations, `2` = validation failed. See `Documentation/command-line-arguments.md`.

Debug shortcut: passing a single arg `-load:<project.json>` auto-loads a saved project after the UI starts (see `App.LoadProjectFileFromCommandLineAsync`).

## Solution layout

Five projects wired together in `CSharpCodeAnalyst.sln`:

- **`CodeGraph/`** — pure, UI-free domain model. Contains the `CodeElement` / `Relationship` / `CodeGraph` graph types (`Graph/`), graph algorithms (`Algorithms/Cycles`, `Algorithms/Metrics`, `Algorithms/Partitioning`), exporters (`Export/` — DGML, DSI, PlantUML, JSON), and `Exploration/CodeGraphExplorer` (traversal queries used from the UI context menus). No WPF dependencies — reference this project from tests and tools.
- **`CodeParser/`** — Roslyn front-end that turns an `.sln` or `.csproj` into a `CodeGraph`. Entry point: `Parser.ParseAsync(path)`. Works in **two passes**: `HierarchyAnalyzer` finds code elements and parent/child links, then `RelationshipAnalyzer.AnalyzeRelationships` walks method and lambda bodies to build relationships (parallel by default; pass `maxDegreeOfParallelism: 1` for a single-threaded debug run). `Initializer.InitializeMsBuildLocator()` **must** be called once before any parse (both `App.StartUi` and the test fixture `Init` do this).
- **`CSharpCodeAnalyst/`** — WPF front-end. Organized by feature under `Features/` (`CycleGroups`, `Graph`, `Tree`, `AdvancedSearch`, `Analyzers/ArchitecturalRules`, `Analyzers/EventRegistration`, `Ai`, `Import`, `Export`, `Metrics`, `Partitions`, `Refactoring`, `Gallery`, `Help`, `Info`). Cross-cutting infrastructure lives in `Shared/` (messaging, notifications, data grid, search, filter, WPF helpers). `Configuration/` holds `AppSettings` (from `appsettings.json`), `UserPreferences` (persisted to `userSettings.json`), and `AiCredentialStorage`. Persistence of saved projects is in `Persistence/` (JSON, with DTOs under `Dto/`).
- **`Tests/`** (project name `CodeParserTests`) — NUnit suite. `ApprovalTests/` parses the `TestSuite/` C# solution once per fixture and asserts on the resulting graph; `UnitTests/` covers cycles, exploration, export, search, architectural rules, etc.
- **`ApprovalTestTool/`** — standalone console app that clones external repos listed in `Repositories.txt`, parses each at a pinned commit, hashes the graph dump, and diffs against references. Used to catch parser regressions on real codebases; not part of the CI test run.

`ThirdParty/DsmSuite/` holds a vendored subset (7 of ~38 projects) of the GPL-licensed DsmSuite, which provides the matrix view on the DSM tab. It is foreign code with its own rules — see **Vendored DsmSuite** below before touching anything in there.

`TestSuite/` is a handcrafted C# solution used purely as parser input for the approval tests. Do not consume it from production code — it is intentionally full of odd language constructs. `ReferencedAssemblies/` contains the MSAGL DLLs referenced directly by `CSharpCodeAnalyst.csproj` and `Tests.csproj` (MSAGL is not on NuGet for the versions used here).

## Architectural notes worth knowing before editing

### MSBuild runtime trap (`Directory.Build.props`)
Every project inherits a `Microsoft.Build.Framework` `PackageReference` with `ExcludeAssets="runtime"`. This is load-bearing: `Microsoft.Build.Locator` loads MSBuild from the installed SDK at runtime, and copying the NuGet-provided MSBuild DLLs into `bin/` causes `RPC_E_CALL_REJECTED` and other loader failures. When upgrading Roslyn / MSBuild packages, keep the exclusion in place and bump the version comment in `Directory.Build.props` to match the transitive version from `Microsoft.Build.Locator`.

### Parser: two passes, then global-namespace fixup
`Parser.ParseSolutionInternal` runs `HierarchyAnalyzer` → `RelationshipAnalyzer` → `InsertGlobalNamespaceIfUsed`. The global-namespace insertion normalizes assemblies that contain types directly at the root (e.g. test assemblies with generated `Main`) so that cycle detection always has a shared ancestor above `Namespace` rather than at `Assembly`. Preserve this invariant if you touch the post-processing.

**Document parser modelling decisions:** when you change how the parser maps C# to the graph — a new/changed relationship, a Roslyn quirk worked around, a deliberate "this looks like X but we model it as Y" choice — add or update a short chapter in `Documentation/Roslyn/corrections-and-updates.md` (English, in the existing style: the construct, why it is tricky, how we model it and the reasoning). This file is the running record of those non-obvious decisions; keep it in sync with parser changes.

### MVVM with a message bus
The UI is not built on a DI container. `App.StartUi` wires up singletons manually (one `MessageBus`, one `CodeGraphExplorer`, one `GraphViewer`, etc.) and injects them into view models. Cross-view-model communication goes through `Shared/Messages/MessageBus.cs` (`Publish`/`Subscribe` on strongly-typed message records in `Shared/Messages/`). When adding a new cross-feature interaction, prefer defining a new message type over introducing direct view-model references.

### Graph rendering
`Features/WebGraph/` replaces the former MSAGL renderer. The graph is displayed in an embedded Chromium browser (`Microsoft.Web.WebView2`) using **Cytoscape.js**. Assets (HTML, CSS, `cytoscape.min.js`, layout extensions) live in `Features/WebGraph/Web/` and are served offline via a WebView2 virtual-host mapping (`https://csharp-code-analyst.local/`). The `Web/` folder (incl. `lib/`) is copied to the output via a `Content Include="Features\WebGraph\Web\**\*"` glob, so new asset files are picked up automatically — no `.csproj` edit needed.

**Adding a third-party library (web `lib/` JS/CSS, NuGet package, or DLL) requires two licensing updates — do not skip these:** (1) add the library to the matching `ThirdPartyNotices/<LICENSE>-LICENSED-LIBRARIES.txt` (grouped by license type; the license text lives once per file, so usually you only append a list entry — create a new file only for a license type not yet present), and (2) add an acknowledgement entry to the **"Thank you"** section of `README.md` (name, license, project URL). Both `ThirdPartyNotices/**` and the README ship with the app, so an omission is a real compliance gap. Always confirm the actual license from the package's own metadata rather than assuming.

**Data flow (C# → JS):** `WebGraphBuilder.Build` converts the current `CodeGraph` + `PresentationState` into a `{nodes, edges}` JSON payload. `WebGraphControl` calls `ExecuteScriptAsync("renderGraph(<json>)")` once JS signals readiness with a `{type:"ready"}` message.

**Compound nodes:** Every `CodeElement.Parent` link maps directly to a Cytoscape `parent` field, so namespaces, classes, and other containers render with their children nested inside — no extra hierarchy logic needed.

**Event routing (JS → C#):** Clicks, double-clicks, right-clicks, and selection changes are `postMessage`-ed to C# via `window.chrome.webview.postMessage(...)` and received in `CoreWebView2.WebMessageReceived`. Context menus are WPF `ContextMenu`s built by `WebContextMenuFactory` from the existing command objects (`ICodeElementContextCommand` / `IRelationshipContextCommand` / `IGlobalCommand`), opened with `PlacementMode.MousePoint` over the WebView2 control.

**Initialisation:** `WebGraphControl` (a `UserControl` wrapping `WebView2`) must be in the WPF visual tree to initialise, so the web tab is tab index 0 in `MainWindow` (eager-init on startup). The WebView2 user-data folder goes to `%LocalAppData%\CSharpCodeAnalyst\WebView2`.

The ~200-element soft limit (`AppSettings.WarningCodeElementLimit`) still applies; Cytoscape's canvas-based renderer handles it comfortably with `fcose` or `dagre` layouts.

### Analyzers (how to add one)
Analyzers are the boxes under the **Analyzers** ribbon button. Each implements `IAnalyzer` (`CSharpCodeAnalyst.AnalyzerSdk/Contracts/IAnalyzer.cs`): `Id` / `Name` / `Description`, an `Analyze(CodeGraph)` that does the work, plus `GetPersistentData` / `SetPersistentData` / `IsDirty` / `DataChanged` (return `null` / no-op / `false` for a stateless analyzer — only the Architectural Rules analyzer actually persists). They live in `CSharpCodeAnalyst.Analyzers/<Feature>/` with the presentation VMs under `<Feature>/Presentation/`. The pure algorithm belongs one layer down in `CSharpCodeAnalyst.CodeGraph/Algorithms/Metrics/` (UI-free, unit-tested directly).

Data flow, end to end:
1. **Algorithm** in `CodeGraph/Algorithms/...` takes the `CodeGraph` and returns a plain result object. Type-level analyses lift relationships to the containing type, deduplicate, and exclude `IsExternal` nodes (see `TypeDependencyAnalysis` / `SystemMetricsAnalysis` as the reference); reuse `Type.IsDependency()` to decide which edges count.
2. **`Analyze`** runs the algorithm; on an empty result it calls `_userNotification.ShowSuccess(...NoData)` and returns, otherwise it builds a **table view model** and publishes `new ShowTabularDataRequest(Id, Name, vm)` on the message bus.
3. **Table VM** derives from `Table` (`AnalyzerSdk/DynamicDataGrid/Contracts/TabularData/`): `GetColumns()` returns `TableColumnDefinition`s (each binds a `PropertyName` on the row VM), `GetData()` returns the `TableRow`s. Optional: `CanFilter`/`Filter`, `GetCommands()` (context-menu / double-click actions), row-details template, and per-column `Rating` (an `IMetricRating` → colored cell background, see `ThresholdRating` and `RatingToBrushConverter`).
4. **Row VM** derives from `TableRow` and exposes one property per column (plus a `SortMemberName`/`RatingValuePropertyName` numeric backer when the displayed column is a formatted string).
5. **Register** the analyzer in `CSharpCodeAnalyst/Features/Analyzers/AnalyzerManager.LoadAnalyzers` (add a `using <Feature> = ...` alias and an `_analyzers.Add`). **No XAML change** is needed: the ribbon `RibbonSplitButton` binds `ItemsSource` to `MainViewModel.Analyzers` (= `AnalyzerManager.All`) and runs `ExecuteAnalyzerCommand` with the analyzer `Id`; `MainViewModel` publishes the result into a `DynamicTab` that hosts a `DynamicDataGrid`.
6. **Strings** live in `CSharpCodeAnalyst.Analyzers/Resources/Strings.resx` **and** its hand-maintained `Strings.Designer.cs` (add the getter yourself). Convention: `Analyzer_<Id>_Label` / `_Tooltip` / `_NoData`, `Column_<Id>_<Col>`.

`SystemMetrics` is the smallest complete example to copy from (system-wide single values in a metric/value/description table).

### Architectural rules (how to add one)

Rules live in `CSharpCodeAnalyst.Analyzers/ArchitecturalRules/Rules/` under a two-level hierarchy. `DependencyRule` constrains relationships between code elements (`DENY` / `RESTRICT` / `ISOLATE` / `ALLOW`) and its violation is a set of relationships. `MetricRule` constrains a measured value; it splits into `SystemMetricRule` (one value for the whole graph, `Measure(SystemMetrics)`, violation carries that value) and `CodeElementMetricRule` (one value per element, `Measure(element, MetricStore)` returning `null` for "not applicable", violation carries the offending elements). Rules are immutable value objects parsed from one line of text — never give them a graph or a `MetricStore`; those belong to the run and are passed to `RuleEngine.Execute`.

A new **metric** rule costs exactly two things: a class deriving from the right base, and one entry in `RuleParser.MetricRuleFactories`. The parser has a single regex for the whole family (`KEYWORD = value`, or `KEYWORD: Pattern = value` for element rules), so it needs no change. The base class supplies the range check via `MinThreshold` / `MaxThreshold`, the floating-point tolerance in `IsViolated`, and the baseline rewrite via `CreateBaselineThreshold` / `CreateRuleText`. Implement `Keyword`, the bounds, `Measure`, `FormatValue` (the value *with* its unit) and `CreateDescription`. Thresholds are expressed in the rule's own unit (percent, lines) — convert from the metric's internal representation exactly once, inside `Measure`. When writing a threshold back (baseline), round **up**, otherwise the rule you just wrote is violated again.

Then wire up the edges: `RuleEngine.Execute` for the evaluation, `RuleViolationViewModel` for the table row and detail lines, `ViolationsFormatter` for the CLI output, `RuleCleaner` if the rule can be dead, `BaselineGenerator.RelaxMetricRules` if it can be baselined, and strings in `Resources/Strings.resx` **plus** its hand-maintained `Strings.Designer.cs`. Document the rule in the "Supported rules" tables of `README.md`. `MaxCyclicityRule` and `MaxLinesRule` are the reference implementations of the two kinds.

### AI Advisor
`Features/Ai/AiClient.cs` talks to any OpenAI-compatible endpoint (including Anthropic, Ollama). Credentials are stored via `Configuration/AiCredentialStorage`. The service is stateless and is invoked from the cycle-group UI to summarize a cycle.

### Approval tests
`Tests/ApprovalTests/ApprovalTestBase` uses `[OneTimeSetUp]` and a static `Init` class to parse `TestSuite.sln` exactly once per test run (path is relative: `..\..\..\..\TestSuite\TestSuite.sln` from the test binary). Expected values are large `HashSet<string>` literals inline in each fixture; when the parser legitimately changes output, dump the actual set with `ApprovalTestBase.DumpRelationships` / `DumpCodeElements` and paste it back in. Do not mock the parser — these tests are the safety net for Roslyn-version upgrades.

### Refactoring simulation is destructive
`Features/Refactoring/` mutates the in-memory `CodeGraph` directly (move / delete elements, cut edges) and there is no undo. Any code path that offers these operations should save or warn first — see existing command handlers before adding new ones.

### Vendored DsmSuite (the DSM tab)
The matrix on the DSM tab is not ours: `ThirdParty/DsmSuite/` is a modified subset of [DsmSuite](https://github.com/ernstaii/dsmsuite.sourcecode), pinned to a commit recorded in `ThirdParty/DsmSuite/README.md`, GPL-3.0-or-later (originally MIT). Our side is `Features/DsmMatrix/`: `CodeGraphToDsmModelBuilder` fills a DsmSuite `IDsmModel` straight from the `TypeGraph` — explicit `parentId`, no DSI file, no name splitting — and `DsmMatrixView` hosts their `MatrixView`.

**The two documents in `ThirdParty/DsmSuite/` have different jobs — keep them apart.** `Dsm.md` is the reader's guide: what you see and how to operate it (axes — row = provider, column = consumer; the depth-coloured blocks; the row indicator bar; the presence map when zoomed out; keyboard and wheel). No class names, no rationale for our changes. `README.md` is the record against upstream: the change table, why each change was made, and the bug list. Read `Dsm.md` before changing anything about the view's semantics and keep it in sync — none of it is discoverable from the UI — but put the reasoning in `README.md`.

**Every change under `ThirdParty/DsmSuite/` costs two things, and neither is optional:** (1) a `Changed <YYYY-MM> for CSharpCodeAnalyst` comment at the site explaining *why*, and (2) a row in the change table in `ThirdParty/DsmSuite/README.md`. GPL §5(a) requires stating what was modified, and that table is the map for ever diffing against upstream again — an undocumented change silently becomes indistinguishable from upstream code. Same rule for the bug list in that README when you find (or fix) a defect in their code.

Two traps that are already documented there and worth knowing before you write code against their API:
- **`DsmApplication` binds `DsmQueries` to the model it is constructed with and never rebinds.** Populate the model *first*, then construct `DsmApplication`. Calling their `LoadModel` (i.e. the file-based import path) leaves every query running against the previous model. This is why `DsmMatrixView.Show` builds everything from scratch.
- **Their `MainViewModel` is the DataContext**, including its editing commands. Those mutate the DSM model only, never the `CodeGraph`, so they cannot corrupt a parse result — but they are live in the context menus.

Their resource dictionaries are merged in `App.xaml` at application scope because their controls resolve them via `StaticResource`. Everything in there is keyed; if you pull in more of their XAML, check for implicit (unkeyed) styles first — those would restyle the whole application.

## Code style

- `.editorconfig` is authoritative and ReSharper-tuned: braces required on all `if`/`foreach`/`while`, max line length 199, expression-bodied accessors preferred, `internal` first in modifier order. Analyzer severities default to `none` — do not add warning-as-error enforcement without discussion.
- Nullable reference types are enabled everywhere; honour the annotations rather than suppressing with `!`.
- Namespaces match folders, rooted under `CSharpCodeAnalyst.*` for every project (e.g. `CSharpCodeAnalyst.CodeGraph.Graph`, `CSharpCodeAnalyst.CodeParser.Parser`). `CodeGraph` is a class inside `CSharpCodeAnalyst.CodeGraph.Graph` — fully-qualify it (`CSharpCodeAnalyst.CodeGraph.Graph.CodeGraph`) in places where the namespace/type collision is ambiguous; existing code already does.
