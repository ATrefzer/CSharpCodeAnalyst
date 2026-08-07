# Sample: CI + multi-source evidence pipeline

This pack turns C# Code Analyst into a **repeatable CI stage** and shows how to merge it with other auditors without pretending any single tool is omniscient.

Copy what you need into your product repo. Nothing here is required for day-to-day desktop use of Code Analyst.

## Quick start (structure-only)

1. Write `architecture.rules.txt` (see [`rules/example-layer.rules.txt`](rules/example-layer.rules.txt)).
2. Drop [`workflows/codeanalyst-validate.example.yml`](workflows/codeanalyst-validate.example.yml) into your `.github/workflows/` (edit solution path / rules path).
3. On a **Windows** runner with the **.NET 10 Desktop** runtime, the job downloads a release, runs `-validate`, and uploads `validation-result.txt`.

Or call the composite action from this repository (after you vendor or reference it):

```yaml
- uses: ./samples/ci-pipeline/action
  with:
    solution: MyApp.sln
    rules: architecture.rules.txt
    output-dir: artifacts/codeanalyst
```

## Multi-source hub (optional)

```text
python samples/ci-pipeline/scripts/parse_validation_result.py \
  --input artifacts/codeanalyst/validation-result.txt \
  --edges-out artifacts/pipeline/edges-codeanalyst.jsonl \
  --handoff-out artifacts/pipeline/handoffs/codeanalyst.json

# Your other importers write more JSONL files with the same edge shape…
python samples/ci-pipeline/scripts/merge_edges_example.py \
  --edges-dir artifacts/pipeline \
  --dashboard-out artifacts/pipeline/dashboard.md
```

Schema: [`schema/edge.schema.json`](schema/edge.schema.json).  
Design doc: [`../../Documentation/ci-and-multi-source-pipeline.md`](../../Documentation/ci-and-multi-source-pipeline.md).

## Requirements

| Piece | Notes |
|-------|--------|
| Windows x64 runner | Code Analyst is `net10.0-windows` |
| .NET 10 Desktop runtime | Headless still loads MSBuildWorkspace / desktop bits |
| SDK on runner | Needed so MSBuildLocator can load your solution |
| Python 3.10+ | Only if you use the parse/merge scripts |

## Honesty bar

- Exit code 0 means **rules clean**, not "product wiring proven."
- Markup-only paths (Avalonia AXAML, some WPF bindings) may contribute **zero** relationships.
- Prefer dual-track audits: structure here, routes elsewhere.
