# CLAUDE‑2.md — Architectural Refactoring Context

**Date**: 2026‑03‑27
**Context**: Architectural assessment of CSharpCodeAnalyst and improvement suggestions for folder structure, dependency management, and potential project splitting.

---

## 1. Current Architecture Summary

### 1.1 Project Structure (as of 2026‑03‑27)

```
/ (root)
├── CSharpCodeAnalyst/          # WPF UI (main executable)
├── CodeGraph/                  # Core graph model & algorithms (domain)
├── CodeParser/                 # Roslyn parser (data ingestion)
├── Tests/                      # Unit tests
├── ApprovalTestTool/           # Approval‑testing tool
├── Contracts/                  # Contains CodeParserTests (leftover)
├── TestSuite/                  # Sample projects for testing
├── CSharpCodeAnalyst.Mcp/      # MCP server (new, references CodeGraph)
├── Documentation/              # Documentation files
├── ReferencedAssemblies/       # Third‑party DLLs (MSAGL)
├── publish/                    # Build output
└── ThirdPartyNotices/          # License notices
```

### 1.2 Dependencies Flow

```
CSharpCodeAnalyst.Mcp ← CodeGraph
CSharpCodeAnalyst ← CodeParser ← CodeGraph
Tests ← all three main projects
```

### 1.3 Internal Organization of UI Project

```
CSharpCodeAnalyst/
├── Analyzers/          # Architectural rules, event registration analyzers
├── Areas/             # UI sections (GraphArea, TreeArea, CycleGroupsArea...)
├── CommandLine/       # CLI processor
├── Common/           # Utilities
├── Configuration/    # Settings classes
├── Export/          # DGML, PlantUML, PNG export
├── Filter/          # Graph filtering
├── Gallery/         # Graph session storage
├── Help/            # Help system
├── Import/          # Solution, jdeps, plain‑text import
├── Messages/        # MessageBus, message types
├── Project/         # Project serialization/deserialization
├── Refactoring/     # Simulated refactorings
├── Shared/          # Common UI components
├── Styles/          # WPF styles
├── Wpf/             # WPF‑specific utilities
└── (21 ViewModels)  # MVVM pattern
```

---

## 2. Proposed Folder Restructuring (Phase 1)

**Goal**: Align with common .NET practices, improve navigation.

```
/ (root)
├── src/                        # Production source code
│   ├── CSharpCodeAnalyst/      # WPF UI (unchanged location)
│   ├── CodeGraph/              # Core graph library
│   ├── CodeParser/             # Roslyn parser
│   └── CSharpCodeAnalyst.Mcp/  # MCP server (part of product)
├── tests/                      # All test projects
│   ├── UnitTests/              # Existing Tests/ project
│   └── ApprovalTestTool/       # Approval‑testing harness
├── testdata/                   # Test fixtures
│   └── TestSuite/              # Sample projects for analysis
├── tools/                      # Build/development utilities (if any)
├── docs/                       # Documentation
│   └── Documentation/          # Move existing folder here
├── lib/                        # External binaries
│   └── ReferencedAssemblies/   # MSAGL DLLs
├── build/                      # Build artifacts (optional)
└── solution/                   # Solution file, .editorconfig, global.json
```

**Special considerations**:
- `CSharpCodeAnalyst.Mcp` stays in `src/` as it's a core component
- `Contracts/` appears to be a leftover; merge `CodeParserTests` into `tests/UnitTests/`
- `ThirdPartyNotices/` stays at root for license compliance
- Update relative project references in `.csproj` files (e.g., `..\CodeGraph` → `..\..\src\CodeGraph`)

---

## 3. Architectural Improvement Suggestions

### 3.1 Low‑Hanging Fruit (Immediate Wins)

**A. Dependency Injection Cleanup**
- **Problem**: Manual wiring in `App.xaml.cs`, `MainViewModel` creates services internally
- **Solution**: Use `Microsoft.Extensions.DependencyInjection`
  ```csharp
  // In App.xaml.cs or Program.cs
  var services = new ServiceCollection();
  services.AddSingleton<IMessageBus, MessageBus>();
  services.AddSingleton<IImporter, Importer>();
  services.AddSingleton<IExporter, Exporter>();
  services.AddSingleton<IProjectService, ProjectService>();
  // Register ViewModels with their dependencies
  ```
- **Benefit**: Better testability, explicit dependencies, lifetime management

**B. Interface Extraction for Services**
- Define `IImporter`, `IExporter`, `IProjectService`, `IAnalyzerManager`, `IRefactoringService`
- Enable mocking and alternative implementations
- Already partially done (`IUserNotification` exists)

**C. Extract Command Handlers**
- **Problem**: `MainViewModel` (1077 lines) contains 20+ command handlers
- **Solution**: Separate command classes:
  ```csharp
  public class LoadSolutionCommand : ICommand
  {
      private readonly IImporter _importer;
      private readonly MainViewModel _viewModel;
      public LoadSolutionCommand(IImporter importer, MainViewModel viewModel) { ... }
      public override void Execute(object parameter) { ... }
  }
  ```

### 3.2 Medium‑Effort Improvements

**A. Logical Project Splitting (Evaluate Need)**

*Option A: Separate Assemblies*
```
CSharpCodeAnalyst/          (executable)
├── References:
│   ├── CSharpCodeAnalyst.Core      (MessageBus, contracts, shared models)
│   ├── CSharpCodeAnalyst.ViewModels (all ViewModels, converters)
│   ├── CSharpCodeAnalyst.Services  (Importer, Exporter, Project, Analyzers)
│   └── CSharpCodeAnalyst.Views     (optional: XAML files)
```

*Option B: Feature‑Based Folders (within same project)*
```
CSharpCodeAnalyst/
├── Import/              # Importer, ImportViewModel, ImportView.xaml
├── Export/              # Exporter, ExportCommands, ExportDialog.xaml
├── Analysis/            # Analyzers, CycleDetection, Metrics
├── Visualization/       # Graph, Tree, InfoPanel
└── ProjectManagement/   # Project serialization, Gallery, Settings
```

**Decision Criteria**:
- Use separate assemblies if: planning CLI version, team >2 developers, compile times problematic
- Keep single project if: single developer, no multiple frontends planned

**B. Plugin Architecture for Analyzers** (long‑term)
- Make analyzers loadable from separate assemblies (MEF or custom plugin system)
- Allow users to add custom analyzers without recompiling main app

### 3.3 Alignment with Volatility‑Based Decomposition

From original CLAUDE.md philosophy:
> *"Modulgrenzen entlang dessen, was sich unabhängig ändert"*
> (module boundaries along what changes independently)

**Apply to CSharpCodeAnalyst**:
- **Changes together**: ViewModels and Views → keep in same project
- **Changes independently**:
  - Import/Export formats (new file types)
  - Analyzers (new analysis rules)
  - Graph algorithms (optimizations, new metrics)
  - UI themes/styles

---

## 4. Specific Code Observations

### 4.1 Current Strengths
- Clear separation: Domain (`CodeGraph`), Ingestion (`CodeParser`), Presentation (`CSharpCodeAnalyst`)
- Message‑based communication (`MessageBus`) reduces coupling
- MVVM pattern with 21 ViewModels
- Logical folder organization within UI project

### 4.2 Current Weaknesses
- `MainViewModel` is too large (1077 lines, 20+ responsibilities)
- Manual service creation and wiring
- Missing interfaces for key services
- Mixing command logic with state management

### 4.3 Risk Areas
- `App.xaml.cs` is a composition root but also handles command‑line mode
- `MainViewModel` creates `Importer`, `Exporter`, `Project` internally (hard to test)
- Direct use of `MessageBus` without abstraction

---

## 5. Recommended Implementation Order

1. **Phase 1**: Folder restructuring (mechanical, low risk)
2. **Phase 2**: Add DI container and interface extraction
3. **Phase 3**: Extract command handlers from `MainViewModel`
4. **Phase 4**: Evaluate need for project splitting based on actual evolution

**Philosophy**: *Measure twice, cut once*. Start with non‑breaking changes (DI, interfaces) before structural splits.

---

## 6. Testing Strategy

**Current**: `Tests/` project references all three main projects.

**After restructuring**:
- Unit tests for `CodeGraph` (domain logic)
- Unit tests for `CodeParser` (parsing logic)
- Unit tests for ViewModels (with mocked services)
- Integration tests for `CSharpCodeAnalyst` (full UI flow, optional)

**Note**: ViewModel testing requires DI and interface extraction first.

---

## 7. MCP Server Integration

**Current**: `CSharpCodeAnalyst.Mcp` references `CodeGraph` only.

**Future considerations**:
- MCP server could use services from `CSharpCodeAnalyst.Services` if split
- Keep MCP dependencies minimal (only `CodeGraph` and `ModelContextProtocol`)
- Consider shared contracts/interfaces between UI and MCP

---

## 8. Overdesign Warning Signs

Avoid splitting when:
1. Single developer maintaining the tool
2. No plans for multiple frontends (CLI, web, etc.)
3. Build complexity increases disproportionately
4. Cross‑assembly refactoring becomes cumbersome

**Remember**: Tool has educational purpose (teaching dependency analysis). Demonstrate *pragmatic* architecture, not perfect architecture.

---

## 9. Next Session Checklist

### High Priority
- [ ] Implement folder restructuring (`src/`, `tests/`, `testdata/`)
- [ ] Add `Microsoft.Extensions.DependencyInjection`
- [ ] Extract interfaces for `Importer`, `Exporter`, `ProjectService`
- [ ] Refactor `App.xaml.cs` to use DI container

### Medium Priority
- [ ] Extract command handlers from `MainViewModel`
- [ ] Evaluate ViewModel testability
- [ ] Consider feature‑based folder reorganization

### Low Priority
- [ ] Assess need for separate assemblies
- [ ] Plan plugin system for analyzers
- [ ] Consider CLI front‑end using shared services

---

## 10. Decision Record

**Folder restructuring approved**: Yes
**DI container approved**: Yes
**Interface extraction approved**: Yes
**Project splitting**: Deferred (evaluate after Phase 1‑3)
**Feature‑based folders**: Optional (consider after restructuring)

**Rationale**: Start with reversible improvements, gather data on actual evolution patterns before making irreversible structural changes.

---
*This document captures architectural context for future refactoring sessions. Update as decisions are made and implemented.*