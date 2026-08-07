# PipelineDocsCli

Write a durable **`.pipeline`** document for a .NET solution: projects, references, types, calls, constructions, events, inputs/outputs — sourced from **MSBuild + Roslyn**, not guesses.

## Why this exists

Interactive tools (including C# Code Analyst itself) are excellent for exploring *now*. Rebuild work needs something else:

1. **Document** what the compiler actually sees  
2. **Debug / reverse-engineer** against that map  
3. **Rebuild** with evidence instead of “the AI assembled something once”

`.pipeline` is intentionally **human-readable** (Markdown-friendly markers, not opaque JSON dumps), deterministic in ordering, and safe to check into `docs/pipeline/` so the architecture stays visible between agents, reviews, and rewrites.

This sample does **not** rewrite source files. It only emits the map.

## Commands

From the CSharpCodeAnalyst repository root (or any solution once you copy this folder):

```powershell
dotnet run --project samples/PipelineDocsCli -- snapshot `
  --repo . `
  --solution CSharpCodeAnalyst.sln `
  --output docs/pipeline/CSharpCodeAnalyst.pipeline
```

Run beside a build without making analysis a gate:

```powershell
dotnet run --project samples/PipelineDocsCli -- `
  alongside build -- CSharpCodeAnalyst.sln
```

| Command | Purpose |
|---------|---------|
| `snapshot` | Produce/replace the `.pipeline` file |
| `alongside build` / `alongside run` | Snapshot in parallel with `dotnet`; keep the original exit code |

## What it traces

- Projects in the solution **and** adjacent `.csproj` / multi-language project files under the repo
- Project references and reverse references
- Output kind / target framework
- Types, inheritance, interfaces
- Method calls and constructed types (semantic symbols when available)
- Event subscriptions, method I/O types
- Diagnostic list when MSBuildWorkspace fails a project (no silent guessing)

## Optional MSBuild annotations

For adjacent projects Roslyn cannot explain:

```xml
<PropertyGroup>
  <PipelineRole>occasional-tool</PipelineRole>
  <PipelinePurpose>Why this project sits outside the main solution graph.</PipelinePurpose>
</PropertyGroup>
```

## How it sits next to C# Code Analyst

| Tool | Job |
|------|-----|
| **C# Code Analyst** (desktop / `-validate`) | Interactive graph, cycles, architectural rules / CI |
| **PipelineDocsCli** (this sample) | Durable map for documentation and rebuild work |

You can still feed Code Analyst rules from layers you discover while reading `.pipeline`. Pair with [CI multi-source pipeline notes](../ci-pipeline/README.md) if you merge several evidence sources.

Full narrative: [Documentation/pipeline-documentation.md](../../Documentation/pipeline-documentation.md).

## Requirements

- .NET 8+ SDK (MSBuildLocator loads the installed MSBuild)
- Solution loadable by SDK-style MSBuild (same constraints as Code Analyst — see Supported projects)

## License

Same as C# Code Analyst (GPL-3.0) when distributed as part of this repository.
