# CI + multi-source evidence pipeline

[TOC]

C# Code Analyst’s headless `-validate` mode is a **structural** auditor: DENY/RESTRICT layers, NOCYCLES, MAXCYCLICITY, LOC budgets. That is exactly what most teams need for CI guardrails.

Used alone, though, `-validate` answers one question ("does this dependency graph violate these rules?"). Large products usually need more than one instrument. This document describes a **multi-source pipeline** pattern: treat Code Analyst as one evidence stage among peers, merge results with explicit **tensions**, and only then form product decisions.

The pattern is intentionally generic. Teams wire different importers (UI binders, DI graphs, shell maps, hotspots). The CI recipe and edge model still work if your only importer is Code Analyst.

Related:

- [Command-line arguments](command-line-arguments.md)
- [Architectural rules](architectural-rules.md)
- Sample pack: [`samples/ci-pipeline/`](../samples/ci-pipeline/README.md)

---

## 1. What Code Analyst proves (and what it does not)

| Proved well | Often invisible |
|-------------|-----------------|
| Layer rules (`DENY Core.** -> UI.**`) | Avalonia / WPF bindings that only live in markup |
| Type-graph cyclicity (`MAXCYCLICITY`, `NOCYCLES`) | DI registrations that never appear as ordinary C# calls |
| ViewModel → View construction edges (when C# constructs views) | Dynamic feature loading, reflection |
| Metric budgets (`MAXLINES`) | Runtime shell tab routes (`DataTemplate` / content presenters) |

**Hard lesson from Avalonia (and similar UI stacks):** the parse log can report  
`Reading XAML references: … (0 relationships)`.  
Shell routes that exist only as markup are **open circuits** in the Roslyn graph. A type with fan-in 0 can still be LIVE in the product.

**Doctrine:** Code Analyst audits **layers and cycles**. Route/bind truth needs a peer auditor (hand-maintained map, UI binding scanner, integration tests). Do not let a green `-validate` exit code declare "wiring is healthy."

---

## 2. Pipeline shape

```text
┌──────────────────┐   ┌───────────────────┐   ┌────────────────────┐
│  Code Analyst    │   │  Peer importers   │   │  Other instruments │
│  -validate rules │   │  (UI, DI, shell)  │   │  (hotspots, tests) │
└────────┬─────────┘   └─────────┬─────────┘   └──────────┬─────────┘
         │                       │                        │
         └───────────┬───────────┴────────────┬───────────┘
                     ▼                        ▼
              handoff JSON (per source)     raw reports
                     │
                     ▼
              merge: edges + tensions (never silent overwrite)
                     │
         ┌───────────┴───────────┐
         ▼                       ▼
    graph.ssot.jsonl        dashboard.md / CI summary
```

### Rules of the merge

1. **Every edge is tagged** with `evidence_source` (e.g. `codeanalyst`, `ui_bindings`, `di_factory`).
2. **No importer deletes another importer’s edges.** When two sources disagree, emit a `tension` edge or status instead of picking a winner.
3. **Always run the full hub you claim to run.** Partial slices look green and lie.
4. **CI may fail on selected kinds only** (e.g. new DENY edges / NOCYCLES) while still *recording* softer sources for humans.

---

## 3. Evidence edge (minimal contract)

A portable edge is a small JSON object. Full sample schema: [`samples/ci-pipeline/schema/edge.schema.json`](../samples/ci-pipeline/schema/edge.schema.json).

```json
{
  "kind": "layer_deny",
  "from": "MyApp.Core.Services.OrderService",
  "to": "MyApp.UI.ViewModels.OrderViewModel",
  "evidence_source": "codeanalyst",
  "status": "DENY",
  "confidence": 0.95,
  "path": "artifacts/codeanalyst/validation-result.txt",
  "note": "DENY Core.** -> UI.**"
}
```

Suggested kinds for Code Analyst output:

| Result block | kind | status |
|--------------|------|--------|
| `DENY` edges | `layer_deny` | `DENY` |
| `NOCYCLES` groups | `cycle` | `WARN` |
| Metric overages | `metric` | `WARN` or `FAIL` |

Peer importers introduce their own kinds (`bind`, `inject`, `shell_route`, …). Merge keeps them all.

---

## 4. CI integration (headless)

### Exit codes (Code Analyst)

| Code | Meaning |
|------|---------|
| 0 | No rule violations |
| 1 | Violations found |
| 2 | Validation failed (load/parse error) |

### Minimal job (release zip)

Windows runners only (app targets `net10.0-windows`).

```yaml
# Replace every path below marked "EDIT ME" with your own before use.
- name: Restore
  run: dotnet restore MyApp.sln # EDIT ME - your solution's path

# Restore alone can leave a WPF/Avalonia solution with cross-project XAML references
# incompletely loadable - see the "Honesty bar" section of samples/ci-pipeline/README.md.
# Skip this for a plain console/library/service solution.
- name: Build
  run: dotnet build MyApp.sln --no-restore # EDIT ME - your solution's path

- name: Download C# Code Analyst release
  shell: pwsh
  run: |
    $url = "https://github.com/ATrefzer/CSharpCodeAnalyst/releases/latest/download/latest-release.zip"
    Invoke-WebRequest $url -OutFile codeanalyst.zip
    Expand-Archive codeanalyst.zip -DestinationPath tools/codeanalyst -Force

- name: Validate architecture
  shell: pwsh
  run: |
    $exe = Get-ChildItem tools/codeanalyst -Filter CSharpCodeAnalyst.exe -Recurse | Select-Object -First 1
    # CSharpCodeAnalyst.exe is a WinExe (GUI subsystem): the plain `& $exe args` call
    # operator does not reliably wait or propagate the exit code when no console is
    # attached, as on a CI runner. Start-Process -Wait -PassThru is the reliable way to
    # invoke it headlessly; -WorkingDirectory covers tool versions that resolve
    # appsettings.json relative to the working directory instead of the exe's own folder.
    $proc = Start-Process -FilePath $exe.FullName -WorkingDirectory $exe.DirectoryName -NoNewWindow -Wait -PassThru -ArgumentList @(
      "-validate",
      "-sln:${{ github.workspace }}/MyApp.sln",                 # EDIT ME - your solution's path
      "-rules:${{ github.workspace }}/architecture.rules.txt",  # EDIT ME - your rules file's path
      "-log-console",
      "-out:${{ github.workspace }}/artifacts/codeanalyst/validation-result.txt"
    )
    if ($proc.ExitCode -ne 0) { exit $proc.ExitCode }
  # Optional: continue-on-error: true when you only want artifacts, not a red build
```

The composite action under [`samples/ci-pipeline/action/`](../samples/ci-pipeline/action/action.yml) packages the validate step (not the restore/build steps - those stay the caller's responsibility) as a reusable step.

### Parse → structured edges

Headless output is currently **human-readable text**. The sample parser  
[`samples/ci-pipeline/scripts/parse_validation_result.py`](../samples/ci-pipeline/scripts/parse_validation_result.py) turns it into JSON edges + a handoff summary suitable for a multi-source merge.

> Wish for upstream later: a first-class `-out-json:<file>` (or SARIF) export would remove the text scrape. Until then, parsers stay tolerant of whitespace noise.

---

## 5. Rules file hygiene for monorepos

1. Discover path prefixes interactively first (Tree View / advanced search), then write `DENY` / `NOCYCLES` with the **exact** assembly + namespace form the graph uses.
2. Wrong prefixes often **silently no-op** (zero matches ≠ "architecture is clean").
3. Start with a small ruleset: one layer DENY, one NOCYCLES root, optional MAXCYCLICITY baseline. Grow after the first CI run produces readable volume.
4. Member-level DENY volume can be large; type-level rollups (sample script) are better dashboards.

Example starter rules live in [`samples/ci-pipeline/rules/example-layer.rules.txt`](../samples/ci-pipeline/rules/example-layer.rules.txt).

---

## 6. Dual-track audit (recommended)

| Track | Owner | Answer |
|-------|-------|--------|
| **Structure** | C# Code Analyst CI | Layers, cycles, metrics |
| **Routes / bindings** | Peer tool or tests | Shell → view → VM → data |

When structure is clean and routes still fail, fix the peer track — do not invent fake DENY rules to stand in for markup.

---

## 7. Sample pack

| Path | Role |
|------|------|
| [`samples/ci-pipeline/README.md`](../samples/ci-pipeline/README.md) | How to copy into your repo |
| [`samples/ci-pipeline/action/action.yml`](../samples/ci-pipeline/action/action.yml) | GitHub composite action |
| [`samples/ci-pipeline/workflows/codeanalyst-validate.example.yml`](../samples/ci-pipeline/workflows/codeanalyst-validate.example.yml) | Drop-in workflow skeleton |
| [`samples/ci-pipeline/scripts/parse_validation_result.py`](../samples/ci-pipeline/scripts/parse_validation_result.py) | Text → JSON edges |
| [`samples/ci-pipeline/scripts/merge_edges_example.py`](../samples/ci-pipeline/scripts/merge_edges_example.py) | Minimal multi-source merge + dashboard |
| [`samples/ci-pipeline/schema/edge.schema.json`](../samples/ci-pipeline/schema/edge.schema.json) | Edge contract |

Fork the pack, replace peer importers with whatever your product needs, keep Code Analyst as the structural stage. Different teams will (and should) assemble the stages differently — that is the point of sharing the pattern broadly.
