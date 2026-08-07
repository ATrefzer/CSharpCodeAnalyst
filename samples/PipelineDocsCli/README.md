# PipelineDocsCli

Two ways to leave pipeline documentation on a codebase — both from the original
pipeline-docs workflow (see Phoenix Visualizer `tools/PipelineDocsCli`).

| # | Command | What it writes |
|---|---------|----------------|
| **1** | `snapshot` | One solution-level **`.pipeline`** map (projects / types / calls) |
| **2** | `headers` | Auto **`// PIPELINE DOCUMENTATION`** banners at the **top of each `.cs` file** |

Use them for: document → reverse-engineer → rebuild with evidence (not chat memory of “AI hand-built this”).

## Option 1 — solution `.pipeline` map

```powershell
dotnet run --project samples/PipelineDocsCli -- snapshot `
  --repo . `
  --solution CSharpCodeAnalyst.sln `
  --output docs/pipeline/CSharpCodeAnalyst.pipeline
```

Refresh beside a build without making analysis a gate:

```powershell
dotnet run --project samples/PipelineDocsCli -- `
  alongside build -- CSharpCodeAnalyst.sln
```

## Option 2 — per-file headers (original file-banner workflow)

Inserts or updates a leading comment block like:

```csharp
// ============================================================================
// PIPELINE DOCUMENTATION (auto-generated)
// ============================================================================
// PIPELINE: This file is called by: ...
// PIPELINE: This file calls: ...
// PIPELINE: Flow: ...
// PIPELINE: Dependencies: ...
// PIPELINE: Output: ...
// PIPELINE: Audio Integration: ...
// PIPELINE: Effect Integration: ...
// ============================================================================
```

Dry-run first (recommended):

```powershell
dotnet run --project samples/PipelineDocsCli -- headers `
  --project-dir CSharpCodeAnalyst.CodeGraph `
  --dry-run `
  --verbose
```

Apply updates:

```powershell
dotnet run --project samples/PipelineDocsCli -- headers `
  --project-dir CSharpCodeAnalyst.CodeGraph `
  --update-existing
```

| Flag | Meaning |
|------|---------|
| `--project-dir` | Required scan root |
| `--files "a.cs b.cs"` | Limit to listed files |
| `--dry-run` | No writes |
| `--max-calls N` | How many call names to keep |
| `--update-existing` | Refresh auto headers (default true) |
| `--replace-manual` | Also overwrite non-auto PIPELINE blocks |
| `--verbose` | Per-file log |

Manual (non-auto) `// PIPELINE DOCUMENTATION` blocks are left alone unless you pass `--replace-manual`.

Path/role heuristics in `PipelineHeuristics.cs` still carry visualizer/studio-oriented defaults (from the original app). Syntax analysis (calls, usings, audio/effect cues) is general; customize heuristics for your monorepo as needed.

## How it sits next to C# Code Analyst

| Tool | Job |
|------|-----|
| **C# Code Analyst** | Interactive graph, cycles, architectural rules / CI |
| **PipelineDocsCli `snapshot`** | Durable solution map for rebuild docs |
| **PipelineDocsCli `headers`** | Per-file orientation banners in source |

Full narrative: [Documentation/pipeline-documentation.md](../../Documentation/pipeline-documentation.md).

## Requirements

- .NET 10 SDK (sample targets `net10.0`; MSBuildLocator loads installed MSBuild)
- SDK-loadable solutions for `snapshot` (same bar as Code Analyst)

## License

Same as C# Code Analyst (GPL-3.0) when distributed as part of this repository.
