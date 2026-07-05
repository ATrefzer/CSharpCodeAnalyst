# Ideas backlog

Collected 2026-07-04. Unordered wish list — items move out of here when they get a concrete design.

## Feature candidates

### 1. Git history as a second data source: hotspots & change coupling

Roslyn only sees *static* coupling. The git history reveals *logical* coupling: classes that are
always committed together although no static dependency exists between them — hidden architecture
erosion that no static tool can find. On top of that: hotspots = change frequency × complexity
(complexity is already collected in the `MetricStore`).

- Data: `git log --name-only`; mapping file → code element already exists via `SourceLocations`.
- Presentation: analyzer tab with a hotspot ranking, optionally co-change edges as an own edge
  type in the Code Explorer.
- Prior art: CodeScene built a commercial product almost entirely on this idea; there is next to
  nothing in the free desktop space.
- **Prior own work: https://github.com/ATrefzer/Insight already implements this kind of
  analysis as a separate application.** Before designing anything here, review Insight and
  decide what to port / merge instead of rewriting ("analysis suite" idea).

## Analyzers / metrics with real meaning

- **Cyclicity percentage**: share of elements per namespace/assembly that are part of an SCC.
  SCCs are already computed — as a single number per module this becomes a health metric that
  can be tracked over time ("cycle debt: 12% → 9%"). Much more tangible than the raw cycle list.
- **Propagation cost** (MacCormack/Baldwin): average share of the system transitively reachable
  from an element. One number for "how tangled is this system", ideal as a trend across imports.

## Usability — small things with large effect

- **Live feedback in the rules dialog**: "pattern matches 37 elements" while typing, per line.
  The post-run warning exists (2026-07); pre-run feedback would complete it. Plus a button
  "insert full path of current tree selection".
- **Generate sample rules from the loaded project** instead of `MyApp.*`: pick two real
  assemblies from the graph. Also fixes that the fictional samples now trigger no-match warnings,
  and the user sees the path format (assembly prefix) with his own code.
- **SARIF output for `-validate`**: GitHub renders SARIF as PR annotations, so rule violations
  appear as comments on the code line in a pull request. Big visibility win for CI usage at
  moderate cost (violations already carry `SourceLocations`).
- **Single-step undo for the refactoring simulation**: one graph clone before each mutation is
  enough. The docs currently warn "cannot be undone" — a single undo step removes the fear and
  makes experimenting attractive.
- **Legend in the Code Explorer**: a toggleable overlay explaining edge types/colors and node
  shapes. The first question of every new user.

## Suggested order

1. Hotspots / change coupling (true differentiator; review Comprehend first, see above).
2. Cyclicity metric on the side — nearly free.
