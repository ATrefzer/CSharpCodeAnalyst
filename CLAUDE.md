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

`TestSuite/` is a handcrafted C# solution used purely as parser input for the approval tests. Do not consume it from production code — it is intentionally full of odd language constructs. `ReferencedAssemblies/` contains the MSAGL DLLs referenced directly by `CSharpCodeAnalyst.csproj` and `Tests.csproj` (MSAGL is not on NuGet for the versions used here).

## Architectural notes worth knowing before editing

### MSBuild runtime trap (`Directory.Build.props`)
Every project inherits a `Microsoft.Build.Framework` `PackageReference` with `ExcludeAssets="runtime"`. This is load-bearing: `Microsoft.Build.Locator` loads MSBuild from the installed SDK at runtime, and copying the NuGet-provided MSBuild DLLs into `bin/` causes `RPC_E_CALL_REJECTED` and other loader failures. When upgrading Roslyn / MSBuild packages, keep the exclusion in place and bump the version comment in `Directory.Build.props` to match the transitive version from `Microsoft.Build.Locator`.

### Parser: two passes, then global-namespace fixup
`Parser.ParseSolutionInternal` runs `HierarchyAnalyzer` → `RelationshipAnalyzer` → `InsertGlobalNamespaceIfUsed`. The global-namespace insertion normalizes assemblies that contain types directly at the root (e.g. test assemblies with generated `Main`) so that cycle detection always has a shared ancestor above `Namespace` rather than at `Assembly`. Preserve this invariant if you touch the post-processing.

### MVVM with a message bus
The UI is not built on a DI container. `App.StartUi` wires up singletons manually (one `MessageBus`, one `CodeGraphExplorer`, one `GraphViewer`, etc.) and injects them into view models. Cross-view-model communication goes through `Shared/Messages/MessageBus.cs` (`Publish`/`Subscribe` on strongly-typed message records in `Shared/Messages/`). When adding a new cross-feature interaction, prefer defining a new message type over introducing direct view-model references.

### Graph rendering
`Features/WebGraph/` replaces the former MSAGL renderer. The graph is displayed in an embedded Chromium browser (`Microsoft.Web.WebView2`) using **Cytoscape.js**. Assets (HTML, CSS, `cytoscape.min.js`, layout extensions) live in `Features/WebGraph/Web/` and are served offline via a WebView2 virtual-host mapping (`https://csharp-code-analyst.local/`).

**Data flow (C# → JS):** `WebGraphBuilder.Build` converts the current `CodeGraph` + `PresentationState` into a `{nodes, edges}` JSON payload. `WebGraphControl` calls `ExecuteScriptAsync("renderGraph(<json>)")` once JS signals readiness with a `{type:"ready"}` message.

**Compound nodes:** Every `CodeElement.Parent` link maps directly to a Cytoscape `parent` field, so namespaces, classes, and other containers render with their children nested inside — no extra hierarchy logic needed.

**Event routing (JS → C#):** Clicks, double-clicks, right-clicks, and selection changes are `postMessage`-ed to C# via `window.chrome.webview.postMessage(...)` and received in `CoreWebView2.WebMessageReceived`. Context menus are WPF `ContextMenu`s built by `WebContextMenuFactory` from the existing command objects (`ICodeElementContextCommand` / `IRelationshipContextCommand` / `IGlobalCommand`), opened with `PlacementMode.MousePoint` over the WebView2 control.

**Initialisation:** `WebGraphControl` (a `UserControl` wrapping `WebView2`) must be in the WPF visual tree to initialise, so the web tab is tab index 0 in `MainWindow` (eager-init on startup). The WebView2 user-data folder goes to `%LocalAppData%\CSharpCodeAnalyst\WebView2`.

The ~200-element soft limit (`AppSettings.WarningCodeElementLimit`) still applies; Cytoscape's canvas-based renderer handles it comfortably with `fcose` or `dagre` layouts.

### AI Advisor
`Features/Ai/AiClient.cs` talks to any OpenAI-compatible endpoint (including Anthropic, Ollama). Credentials are stored via `Configuration/AiCredentialStorage`. The service is stateless and is invoked from the cycle-group UI to summarize a cycle.

### Approval tests
`Tests/ApprovalTests/ApprovalTestBase` uses `[OneTimeSetUp]` and a static `Init` class to parse `TestSuite.sln` exactly once per test run (path is relative: `..\..\..\..\TestSuite\TestSuite.sln` from the test binary). Expected values are large `HashSet<string>` literals inline in each fixture; when the parser legitimately changes output, dump the actual set with `ApprovalTestBase.DumpRelationships` / `DumpCodeElements` and paste it back in. Do not mock the parser — these tests are the safety net for Roslyn-version upgrades.

### Refactoring simulation is destructive
`Features/Refactoring/` mutates the in-memory `CodeGraph` directly (move / delete elements, cut edges) and there is no undo. Any code path that offers these operations should save or warn first — see existing command handlers before adding new ones.

## Code style

- `.editorconfig` is authoritative and ReSharper-tuned: braces required on all `if`/`foreach`/`while`, max line length 199, expression-bodied accessors preferred, `internal` first in modifier order. Analyzer severities default to `none` — do not add warning-as-error enforcement without discussion.
- Nullable reference types are enabled everywhere; honour the annotations rather than suppressing with `!`.
- Namespaces match folders. `CodeGraph.Graph.CodeGraph` is a class inside the `CodeGraph` assembly — fully-qualify it (`CodeGraph.Graph.CodeGraph`) in places where the namespace/type collision is ambiguous; existing code already does.
