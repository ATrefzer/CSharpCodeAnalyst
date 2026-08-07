# Sample: CI + multi-source evidence pipeline

This pack turns C# Code Analyst into a **repeatable CI stage** and shows how to merge it with other auditors without pretending any single tool is omniscient.

Copy what you need into your product repo. Nothing here is required for day-to-day desktop use of Code Analyst.

## Quick start (structure-only)

1. Write `architecture.rules.txt` (see [`rules/example-layer.rules.txt`](rules/example-layer.rules.txt)).
2. Drop [`workflows/codeanalyst-validate.example.yml`](workflows/codeanalyst-validate.example.yml) into your `.github/workflows/` and edit every line marked `# EDIT ME` in it (your solution path, rules path, default branch name).
3. On a **Windows** runner with the **.NET 10 Desktop** runtime, the job restores, builds, downloads a release, runs `-validate`, and uploads `validation-result.txt`.

Or call the composite action from this repository (after you vendor or reference it). Two
things it deliberately leaves to your own workflow, same as step 2 above:

- **Restore/build the solution first** - the action does not do this for you (see "Honesty
  bar" below for when a real `dotnet build`, not just `restore`, actually matters).
- **Upload the results afterward** - the action only writes files into `output-dir` on the
  runner's local disk; nothing is kept once the job ends unless you add your own
  `actions/upload-artifact` step, same as the `Upload results` step in the example workflow.

```yaml
- run: dotnet restore MyApp.sln            # EDIT ME - your solution's path
- run: dotnet build MyApp.sln --no-restore # EDIT ME - your solution's path

- uses: ./samples/ci-pipeline/action
  with:
    solution: MyApp.sln           # EDIT ME - your solution's path
    rules: architecture.rules.txt # EDIT ME - your rules file's path
    output-dir: artifacts/codeanalyst

- if: always()
  uses: actions/upload-artifact@v4
  with:
    name: codeanalyst-validation
    path: artifacts/codeanalyst/
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
- `dotnet restore` alone can be enough to load a plain solution, but a WPF/Avalonia project
  that references a custom control from another project's XAML needs that project actually
  **built** first - otherwise the affected files fail to load and the graph silently drops
  whatever they would have contributed, with no red flag unless you check the log. The
  example workflow builds by default for this reason; see the comment on its `Build` step
  for when it's safe to skip.
- Prefer dual-track audits: structure here, routes elsewhere.
