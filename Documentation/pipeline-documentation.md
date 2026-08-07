# Pipeline documentation (write maps for reverse-engineering)

[TOC]

Most dependency tools help **explore a live model**. Rebuild work needs something they often skip: a **durable architecture document** written from the compiler’s graph so you can walk:

**document → debug / reverse-engineer → rebuild**

…instead of re-discovering a codebase only through chat transcripts or “AI hand-built this once.”

This repository includes a sample tool that does exactly that writer job:

- **Sample:** [`samples/PipelineDocsCli/`](../samples/PipelineDocsCli/README.md)
- **Output format:** UTF-8 text files ending in `.pipeline` under `docs/pipeline/`

It complements C# Code Analyst. It does not replace the Code Explorer, cycle tools, DSM, or headless `-validate` rules.

---

## 1. What problem this solves

| Situation | Bad outcome | Better outcome |
|-----------|-------------|----------------|
| Agent or human added features without a map | Next session reinvents structure | Checked-in `.pipeline` is the first read |
| Large monorepo, multi-project | “Where does X live?” burns days | Project tree + type tree in one file |
| Reverse-engineering a shipped app | Scattering greps | Ordered call / create / inject-ish hints from Roslyn |
| Rebuild in another shape | Guessing layers | Documented membership, adjacency, purpose annotations |

`.pipeline` files are **not** proof of runtime wiring (reflection, DI containers, XAML/Avalonia bindings can still be invisible — same honesty bar as the Roslyn graph). They *are* proof of what MSBuild and semantic analysis could resolve at snapshot time, with diagnostics kept visible when load failed.

---

## 2. Quick start

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

Copy `samples/PipelineDocsCli` into any other monorepo and point `--solution` / `--output` at your tree.

---

## 3. Format sketch

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

---

## 4. Intent annotations

Roslyn cannot invent *why* an adjacent tool project exists. Optional properties survive into the document:

```xml
<PropertyGroup>
  <PipelineRole>occasional-tool</PipelineRole>
  <PipelinePurpose>Offline import utility; not part of ship loop.</PipelinePurpose>
</PropertyGroup>
```

---

## 5. How this relates to headless Code Analyst CI

| Stage | Owner |
|-------|--------|
| Document structure for humans/agents | **PipelineDocsCli** (`.pipeline`) |
| Enforce DENY / NOCYCLES / metrics in CI | **CSharpCodeAnalyst.exe -validate** |
| Optional multi-source merge (bindings, DI, shell) | [CI multi-source pack](ci-and-multi-source-pipeline.md) |

A healthy rebuild loop often uses all three. Start with the document writer if your gap is “I cannot see what was built.”

Related sample: if you already have a `.pipeline` snapshot, the [multi-source CI pack](../samples/ci-pipeline/README.md) can treat it as one evidence feed beside `-validate` layer rules.

---

## 6. Provenance note

The snapshot CLI and `.pipeline` writer pattern came from real monorepo work: **document the apps you build so later you (or another agent) can reverse-engineer and rebuild from structural evidence**, not from chat memory of “AI hand-built this.” Donated here so C# Code Analyst users can adopt that writer without adopting any particular product stack.

Example excerpt: [`samples/PipelineDocsCli/examples/ExampleApp.pipeline`](../samples/PipelineDocsCli/examples/ExampleApp.pipeline).

