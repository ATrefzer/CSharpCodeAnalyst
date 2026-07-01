# Metrics

This document explains the metrics computed by C# Code Analyst. 

I don’t want a vast number of random metrics; I just want a few useful ones that focus on specific questions.

## Dependency Hotspots

The goal of these metrics is to answer the question you have when facing an unfamiliar codebase: **which types should I look at first?**

Available via *Analyzers → Dependency hotspots*. The result is a sortable table with one row per
type:

| Column  | Meaning                                                      |
| ------- | ------------------------------------------------------------ |
| #       | Rank position when sorted by Score (descending).             |
| Type    | The fully qualified type name.                               |
| Fan-in  | How many other types depend on this type.                    |
| Fan-out | How many other types this type depends on.                   |
| Score   | Transitive importance (PageRank), normalized so the average is 1.0.<br />How much does the the rest of the codebase rests on a type. |

Double-click a row (or right-click → *Show in Code Explorer*) to place the type on the canvas and
start exploring from there.

### The type-level graph

The parsed code graph contains fine-grained relationships: a *method* calls another *method*, a
*field* has a *type*, and so on. Hotspot metrics are not computed on those raw relationships. Every
relationship is first **lifted to the type that contains its endpoints**:

- A call `A.DoWork()` → `B.Helper()` becomes a type edge `A → B`. 
- Relationships above the type level (namespace, assembly) have no containing type and are ignored.

This is deliberate. For understanding architecture, "does class A depend on class B?" is the useful
question — not "which of A's methods calls which of B's methods".

After lifting, the type edges are **deduplicated**. If class `A` calls ten different methods on
class `B`, that is a single `A → B` edge.

This is intentional. **Dependency is a yes/no fact in this context.** For the question "must I understand B in order to understand
A?", the answer does not change whether A touches B in five places or fifty. A depends on B, full
stop.

Note: The only quantity that genuinely exists statically is the number of distinct **call sites**
(`SourceLocations` in the model) or the number of member-to-member edges between two types. That is
a measure of *how entangled* two types are — relevant for estimating decoupling effort,
but a different question from centrality. 

### Which relationships count as a dependency

Most relationship kinds express "the source depends on the target" and all point in the same
direction (dependent → dependency), so they are treated as equal, unweighted edges:

`Calls`, `Creates`, `Uses`, `Inherits`, `Implements`, `Overrides`, `UsesAttribute`, `Invokes`.

Three kinds are **excluded**:

- **Containment** — the parent/child hierarchy (namespace contains class, class contains method).
  This is structure, not a dependency. 
- **Bundled** — artificial edges the UI creates to fold several relationships together for display.
  Not part of the real code.
- **Handles** — an event-handler registration. The model stores it as `handler → event`, but this
  is the reverse callback wiring (the event later calls the handler at runtime), not a compile-time
  dependency of the handler on the event. The real dependency created by `someEvent += OnHandler;` — the subscriber referencing the event — is
  already captured as a `Calls`/`Uses` edge, so nothing is lost by dropping `Handles`.

### External types are excluded

Types defined outside the analyzed solution (framework, NuGet packages) are excluded from the result
and from the edges. Otherwise ubiquitous types like `object` or `string` would dominate the Fan-in
ranking without telling you anything about *your* architecture.

## Fan-in and Fan-out

- **Fan-in** (afferent coupling) = the number of distinct types that depend on this type.
- **Fan-out** (efferent coupling) = the number of distinct types this type depends on.

They answer two different questions and both are worth reading:

- **High Fan-in** → many things rest on this type. It is *foundational*. Changing it is risky
  (ripple effect), and it is usually worth understanding early.
- **High Fan-out** → this type knows about many others. It is an *orchestrator* or a potential
  god-class. It is also a good entry point, but for the opposite reason: it tells you *what the
  system does*, not *what it is built on*.

## Score (PageRank)

Fan-in alone has a blind spot: it treats every incoming dependency as equal. A logging utility used
by 200 trivial classes gets a huge Fan-in, but it is not an architecturally important type — it is
just ubiquitous. Conversely, a core domain type used by only a handful of *central* types can be
more important than its raw Fan-in suggests.

**Score** fixes this by measuring *transitive* importance: a type is important when **important
types depend on it**, not merely when many types do. This is the PageRank algorithm, the same idea
Google uses to rank web pages.

(!) In everyday terms: on the web, PageRank estimates how likely you are to reach a page by following
links; here it estimates how much of the rest of the codebase ultimately rests on a type — directly,
and through the other types that rest on it. A high Score means a lot of your code leans on this
type, so it is both what you most need to understand and what is riskiest to change.

### How it is computed

PageRank is computed by power iteration on the type-level graph:

```
PR(v) = (1 - d) / N  +  d · Σ  PR(u) / outdegree(u)
                          u → v
```

- `N` = number of types, `d` = damping factor (0.85).
- Edges are **not reversed.** Rank flows along `A → B` edges toward the depended-upon type `B`, so
  foundational types (base classes, interfaces, core services) accumulate rank and rise to the top.
- **Dangling types** (no outgoing edges, e.g. DTOs etc) would leak rank out of the system; their rank is
  redistributed uniformly across all types each iteration.
- Iteration stops when the values converge (total change < 1e-6) or after 100 iterations.

The raw PageRank values form a probability distribution: they sum to 1 over all types. On a large
solution each raw value is therefore tiny and hard to interpret.

### PageRank × N (average = 1.0)

Because the raw values sum to 1 across `N` types, the **average** raw value is `1/N`. Multiplying
every value by `N` rescales the average to exactly 1.0:

```
average Score = (1 / N) · N = 1
```

This makes Score a size-independent, relative number:

- **Score = 1.0** → an average type.
- **Score = 5.0** → five times more central than the average type.
- **Score < 1.0** → below-average centrality (most leaf types).

## How to read the table

1. Sort by **Score** (default) to find the types the rest of the code leans on most. These carry
   the most weight — the highest payoff to understand and the highest risk to change. That is not
   automatically where you start reading: it depends on your approach. Working bottom-up, these
   foundational types are the vocabulary of the system and a natural starting point; working
   top-down, you may prefer to start at the orchestrators below and drill down into them.
2. Sort by **Fan-out** to find the big orchestrators — read these to learn what the system *does*.
3. Watch for the disagreement between Fan-in and Score: a type with high Fan-in but modest Score is
   a widely-used utility (a logger, an extension-method holder); a type with modest Fan-in but high
   Score is a genuine architectural core. That gap is often the most informative signal in the
   table.

## Limitations

- Metrics are structural only. They say nothing about code quality, correctness, or how hard a type
  is to read internally — only about its position in the dependency graph.
- Dependencies are counted as yes/no; the strength or frequency of a coupling is not modeled.
- External types are excluded, so a type whose real importance comes from being called by framework
  callbacks (e.g. a controller invoked only by ASP.NET) may rank lower than its runtime role.
