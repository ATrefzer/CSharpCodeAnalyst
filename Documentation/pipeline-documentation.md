# Pipeline documentation (write maps for reverse-engineering)

[TOC]

Most dependency tools help **explore a live model**. Rebuild work needs something they often skip: durable documentation written from the code so you can walk:

**document → debug / reverse-engineer → rebuild**

…instead of re-discovering a codebase only through chat transcripts or “AI hand-built this once.”

This repository includes a sample tool with **two complementary writers**:

| Mode | Command | Artifact |
|------|---------|----------|
| **1 — Solution map** | `snapshot` | One human-readable **`.pipeline`** file |
| **2 — Per-file banners** | `headers` | **`// PIPELINE DOCUMENTATION`** headers at the top of each `.cs` file |

- **Sample:** [`samples/PipelineDocsCli/`](../samples/PipelineDocsCli/README.md)
- **Default map path:** `docs/pipeline/<SolutionName>.pipeline`

It complements C# Code Analyst. It does not replace the Code Explorer, cycle tools, DSM, or headless `-validate` rules.

---

## 1. What problem this solves

| Situation | Bad outcome | Better outcome |
|-----------|-------------|----------------|
| Agent or human added features without a map | Next session reinvents structure | Checked-in `.pipeline` + file banners |
| Large monorepo, multi-project | “Where does X live?” burns days | Project tree + type tree in one file |
| Opening a random `.cs` mid-rewrite | No orientation | Auto header: called by / calls / flow |
| Reverse-engineering a shipped app | Scattering greps | Ordered call / create hints from Roslyn + path roles |
| Rebuild in another shape | Guessing layers | Documented membership, adjacency, purpose annotations |

`.pipeline` files and auto headers are **not** proof of every runtime wire (reflection, DI containers, XAML/Avalonia bindings can still be invisible — same honesty bar as the Roslyn graph). They *are* bookmarks of what static analysis + folder roles could report, so rebuild work starts with evidence.

---

## 2. Quick start — option 1 (`snapshot`)

```powershell
# From this repository root
dotnet run --project samples/PipelineDocsCli -- snapshot `
  --solution CSharpCodeAnalyst.sln `
  --output docs/pipeline/CSharpCodeAnalyst.pipeline
```

Or refresh beside a build without blocking it:

```powershell
dotnet run --project samples/PipelineDocsCli -- `
  alongside build -- CSharpCodeAnalyst.sln
```

---

## 3. Quick start — option 2 (`headers`)

The original **per-file** pipeline banners:

```powershell
# Dry-run first
dotnet run --project samples/PipelineDocsCli -- headers `
  --project-dir CSharpCodeAnalyst.CodeGraph `
  --dry-run --verbose

# Write auto-generated headers (skips non-auto manual blocks)
dotnet run --project samples/PipelineDocsCli -- headers `
  --project-dir CSharpCodeAnalyst.CodeGraph
```

Header shape (auto-generated marker so rewrites stay safe):

```csharp
// ============================================================================
// PIPELINE DOCUMENTATION (auto-generated)
// ============================================================================
// PIPELINE: This file is called by: …
// PIPELINE: This file calls: …
// PIPELINE: Flow: …
// PIPELINE: Dependencies: …
// PIPELINE: Output: …
// PIPELINE: Audio Integration: …
// PIPELINE: Effect Integration: …
// ============================================================================
```

Notes:

- Non-auto `// PIPELINE DOCUMENTATION` blocks are **not** replaced unless you pass `--replace-manual`.
- Path heuristics still include visualizer/studio-oriented defaults from the original monorepo tool; syntax call extraction is general. Tune `PipelineHeuristics.cs` for your product vocabulary.

---

## 4. `.pipeline` format sketch

Markers are line-oriented and stable enough for scripts:

```text
PIPELINE 2.0

REPOSITORY MyApp
SOLUTION MyApp.sln
GENERATED 2026-08-07T15:00:00.0000000+00:00

SUMMARY
  projects-total: …
END SUMMARY

SOLUTION TREE
  MyApp.Core
    -> …
END SOLUTION TREE

PROJECT MyApp.Core
  path: src/MyApp.Core/MyApp.Core.csproj
  membership: solution
  classification: solution-member
  CODE TREE
    TYPE MyApp.Core.Services.OrderService
      kind: Class
      file: src/MyApp.Core/Services/OrderService.cs
      calls:
        - …
    END TYPE
  END CODE TREE
END PROJECT
```

Ordering is deterministic so diffs stay reviewable.

Example excerpt: [`samples/PipelineDocsCli/examples/ExampleApp.pipeline`](../samples/PipelineDocsCli/examples/ExampleApp.pipeline).

---

## 5. Intent annotations (snapshot)

Roslyn cannot invent *why* an adjacent tool project exists. Optional properties survive into the document:

```xml
<PropertyGroup>
  <PipelineRole>occasional-tool</PipelineRole>
  <PipelinePurpose>Offline import utility; not part of ship loop.</PipelinePurpose>
</PropertyGroup>
```

---

## 6. How this relates to headless Code Analyst CI

| Stage | Owner |
|-------|--------|
| Solution map for humans/agents | **PipelineDocsCli `snapshot`** |
| Per-file orientation in source | **PipelineDocsCli `headers`** |
| Enforce DENY / NOCYCLES / metrics in CI | **CSharpCodeAnalyst.exe -validate** |
| Optional multi-source merge (bindings, DI, shell) | [CI multi-source pack](ci-and-multi-source-pipeline.md) |

A healthy rebuild loop often uses all of the above. Start with `snapshot` + `headers` if your gap is “I cannot see what was built.”

---

## 7. Provenance note

The **per-file PIPELINE header workflow** and later the **solution `.pipeline` snapshot** writer are the same documentation practice: leave structural commentary the next human or agent can walk when reverse-engineering or rebuilding. Sources live under product monorepos (e.g. Phoenix Visualizer `tools/PipelineDocsCli`); this sample is donated so C# Code Analyst users can adopt the workflow without depending on any particular app tree.
