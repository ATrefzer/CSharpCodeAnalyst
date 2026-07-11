# Metrics

This guide explains the metrics that C# Code Analyst calculates.

Rather than providing many different metrics, C# Code Analyst focuses on a few that answer specific questions.

C# Code Analyst provides four analyses:

- **Type Dependencies** helps you find the most important and riskiest types in a solution.
- **Type Cohesion** helps you find classes that are doing too many unrelated things and may need to be split.
- **Method Complexity** helps you find the largest and most complicated methods.
- **System Metrics** describes the codebase as a whole with a single value per metric.

These analyses help you understand a new codebase and find possible design issues.

Type Dependencies and Type Cohesion focus on types, while Method Complexity examines methods.
System Metrics looks at the entire system. You can access all these analyses from the *Analyzers* ribbon. Their results appear in a new tab, and for cohesion, you can see more details in the Partitions tab.

## Type Dependencies

The goal of these metrics is to answer common questions when you are working with an unfamiliar codebase:
**Which types should I look at first?** And **how risky is it to change this one?**

Available via *Analyzers → Type Dependencies*. The result is a sortable table with one row per type:

| Column       | Meaning                                                      |
| ------------ | ------------------------------------------------------------ |
| #            | Rank position when sorted by Score (descending).             |
| Type         | The fully qualified type name.                               |
| Fan-in       | How many other types depend on this type.<br />direct (depth 1) |
| Blast radius | How many other types transitively depend on this type — its change impact.<br />transitive, counted |
| Score        | Transitive importance (PageRank), normalized so the average is 1.0. How much the rest of the codebase rests on this type.<br />transitive, weighted by importance |
| Fan-out      | How many other types this type depends on.<br />direct (depth 1) |

Types defined outside the analyzed solution (e.g., frameworks and NuGet packages) are excluded. Otherwise, ubiquitous types like `object` or `string` would dominate the Fan-in ranking.

Here's a quick note about counting dependencies: If 10 methods in class `A` call 5 methods in class `B`, and `A` also accesses a field in `B`, this still counts as one type dependency: `A → B`. **Dependency is simply yes or no here.** If you wonder, "Do I need to understand B to understand A?", the answer is the same whether A interacts with B five times or fifty. A depends on B, that's it.

Relevant relationship types are `Calls`, `Creates`, `Uses`, `Inherits`, `Implements`, `Overrides`, `UsesAttribute`, `Invokes`. You may have seen a `Handles` relationship in the code graph. This is ignored. It is a special relationship introduced to show which method handles an event. In terms of dependencies, this is the wrong direction. The dependency is recognized when the event is registered, however.

### Fan-in and Fan-out

- **Fan-in** = the number of distinct types that depend on this type. Also known as afferent coupling.
- **Fan-out** = the number of distinct types this type depends on. Also known as efferent coupling.

These metrics answer two different questions, and both are useful to consider:

- **High Fan-in** means many things rest on this type. It is *foundational*. Changing it is risky (with ripple effects), and it is usually worth understanding early.
- **High Fan-out** means this type knows about many others. It acts as an *orchestrator* or could be a god-class. This makes it a good starting point for understanding how the system's behavior is coordinated, rather than which types are its foundation.

The extreme cases can also tell you a lot:

- **Fan-in = 0** means nothing depends on this type. It could be an *entry point* (like `Main`, a controller, or a top-level command/handler) or just *dead code*. Either way, it's worth checking.
- **Fan-out = 0** means this type doesn't depend on anything else in your solution. It's a *pure leaf*. Maybe a value type, enum, DTO, or a self-contained foundation. These types form the stable base of the graph.

### Blast radius

**Blast radius** shows how many other types might be affected if you change a type (including indirect effects). The bigger the number, the more carefully you should consider making changes. While Fan-in only counts direct dependents, blast radius tracks all the way through the dependency chain.

This metric answers a simple, practical question: **how risky is it to change this?** A type with a blast radius of 3 is much safer to refactor than one with a blast radius of 800, which could affect most of the codebase.

Blast radius is a **flat count**: every type that can reach you counts as one, regardless of its importance. That differs from Score — which weights each dependent by its importance. A type used by 500 trivial types has a large blast radius but only a moderate Score. The type itself is never counted in its own blast radius.

Blast radius is always **≥ Fan-in** (direct dependents are a subset of transitive ones). When the two differ greatly — small Fan-in, large blast radius — the type sits *deep*: few types touch it directly, but those few carry its influence across much of the codebase.

### Score (PageRank)

Fan-in, by itself, has a limitation: it treats every incoming dependency the same way. For example, a logging utility used by 200 simple classes will have a high Fan-in but is not actually important to the architecture—it's just used everywhere. On the other hand, a core domain type used by only a few key types can be more important than its Fan-in number shows.

**Score** addresses this limitation by measuring *transitive* importance: a type is important when **important types depend on it**, not merely when many types do. This is the PageRank algorithm, the same idea Google originally used to rank web pages.

(!) In everyday terms: on the web, PageRank estimates how likely you are to reach a page by following links; here it estimates how much of the rest of the codebase rests on a type — directly and through other types that rest on it. A high Score means much of your code leans on this type, so it is both what you most need to understand and what is riskiest to change.

Score is the PageRank value normalized so the **average type scores 1.0**:

- **Score = 1.0** → an average type.
- **Score = 5.0** → five times more central than the average type.
- **Score < 1.0** → below-average centrality (most leaf types).

### Reading the numbers

1. Sort by **Score** (default) to find the types the rest of the code leans on most. These carry the most weight — the highest payoff to understand and the highest risk to change. Working from the bottom up, these foundational types are the system's vocabulary and a natural starting point. Working top-down, you may prefer to start with the orchestrators below and drill down into them.
2. Sort by **Fan-out** to find the major orchestrators — these often provide a good overview of how the system's behavior is coordinated.
3. Sort by **Blast radius** before a refactoring — it tells you how far the ripples of a change to a type will reach.
4. Look for differences between Fan-in and Score. A type with high Fan-in but a modest Score is usually a widely used utility, such as a logger or an extension-method holder. A type with a modest Fan-in but a high Score is likely a true architectural core. This gap is often the most useful signal in the table.
5. **High Score and high Fan-out together** is the riskiest combination. This type is both foundational (many things depend on it) and an orchestrator (it interacts with many others). These are often god classes that are hard to change without affecting a lot of code, so they are the first candidates to split up.

### How it is computed

PageRank is computed by power iteration on the type-level graph:

```
PR(v) = (1 - d) / N  +  d · Σ  PR(u) / outdegree(u)
                          u → v
```

- `N` = number of types, `d` = damping factor (0.85).
- Rank flows along `A → B` edges toward the depended-on type `B`, so foundational types (base classes, interfaces, core services) accumulate rank and rise to the top.

The raw PageRank values form a **probability distribution**: they sum to 1 over all types, so on a large solution each value is tiny. The **Score** shown in the table is that value multiplied by `N` (the number of types), which rescales the average to exactly 1.0:

```
average Score = (1 / N) · N = 1
```

### Limitations

- Metrics are structural only. They say nothing about code quality, correctness, or how hard a type is to read internally — only about its position in the dependency graph.
- Dependencies are counted as yes/no; the strength or frequency of a coupling is not modeled.
- External types are excluded, so a type whose real importance comes from being called by framework callbacks (e.g., a controller invoked only by ASP.NET) may rank lower than its runtime role. However, counting externals at the type level would immediately let objects, strings, etc. dominate.


## Type Cohesion

Where *Type Dependencies* looks at a class from the outside, **Type Cohesion** looks *inside* a class and answers a different question: **does this class contain multiple independent responsibilities and if so, how many?**

Available via *Analyzers → Type Cohesion*. The result is a sortable table listing only the classes that are split candidates:

| Column     | Meaning                                                      |
| ---------- | ------------------------------------------------------------ |
| Class      | The full path to the class.                                  |
| Partitions | Into how many independent groups the class's members fall.   |
| Members    | Number of direct members in the partition — size / priority context. |
| Largest %  | Share of the members that sit in the biggest partition (see below). |

Double-click a row to open its partitions in the *Partitions* tab.

### What a partition is

The members of a class (methods, fields, properties, etc.) are **connected** when one calls another or they use the same field. Members are in the same *partition* when they work together. If two groups never interact, they end up in different *partitions*.

If a class has multiple partitions, it often means the class has several responsibilities.

- **1 partition** → fully cohesive: everything is interconnected. (These classes are *not* listed.)
- **N ≥ 2 partitions** → the class is really N separable units. It could be split into N smaller, more focused classes.

This is the connected-components view of cohesion (LCOM4).

Only **classes** are analyzed, not structs, records, or interfaces. Pure data holders are skipped. If a class has fewer than two methods, it doesn't have enough behavior for cohesion to matter, and would otherwise appear maximally "incohesive" (each field would be its own partition).

### Base classes are folded in

Methods in a class can be linked through members inherited from a base class.

So, base-class members are included as **connectors**: they link the class's own members that interact through inherited state or behavior. However, they are then **left out** of the reported partitions, since splitting only concerns the members that belong to this class. External base classes are ignored.

### Reading the numbers

**Partitions** shows you if a class can be split up, while **Largest %** tells you if it's worth splitting. Two classes with the same number of partitions can be very different:

| Class | Partitions | Members | Largest % | Reading                                                      |
| ----- | ---------- | ------- | --------- | ------------------------------------------------------------ |
| X     | 2          | 19      | 53 %      | 10 vs 9 — a class that contains two distinct responsibilities. |
| Y     | 2          | 19      | 95 %      | 18 vs 1 — one cohesive blob plus a stray helper.             |

**Largest %** is the size of the biggest partition divided by the total number of members:

- If **Largest %** is close to 100%, there is one main group and only a few outliers. Splitting is easy (just remove one method), so it's a low priority.
- If **Largest %** is close to 1 divided by the number of partitions (evenly divided), the class splits into balanced, separate responsibilities. This is a strong candidate for refactoring and should be a high priority.

Look at all three columns together:

- **Many Partitions + many Members + low Largest %** → the worst offenders: a big class that genuinely breaks into several balanced, separate parts. Sort by **Largest % ascending** to bring these to the top, and use **Members** to pick the higher-priority candidates among them.
- **High Partitions + high Largest %** → the opposite shape: one solid core and many tiny, unrelated helpers. You can peel these off one at a time rather than doing a big split.
- **Few Members with two or more partitions** means it's low stakes. The class is technically incoherent, but it's too small to worry about.

### Drilling into the partitions

Double-click a row (or right-click and choose *Show partitions*) to open the specific groups in the *Partitions* tab. This lets you see right away which members would go where.

### When a god class still shows one partition

A good example is a WPF/MVVM view model. Each observable property setter calls the same 
`OnPropertyChanged` helper, so all of them are linked through that one method. On top of that, most handlers read the same infrastructure fields — a message bus, a UI notification service, a busy state flag. Those shared members act as connectors among all features. The result: a 1000-line view model with a dozen unrelated commands can report a **single partition** even though it clearly does many separate things.

So, a single partition only shows high structural cohesion. This means no group of members is *structurally* disconnected and pervasive plumbing prevents that almost by design. When you know a class is large and does many unrelated things but still shows one partition, it's probably a hub. Look for a member that is called by almost everything (like `OnPropertyChanged`) or reads (like a shared service).

The partitioner measures how connected members are, not whether they are conceptually related. A 'God Object' that centralizes shared infrastructure (like a message bus, UI notifier, or busy state) can look cohesive by this metric. This is where the two views differ: conceptually, the class has low cohesion, but structurally, it appears highly connected due to shared infrastructure.

Note that you can use the simulated Refactoring feature to eliminate hubs from the model before analysis.

### Limitations

- Only in-solution base classes are folded in; members inherited from framework types are not visible to the analysis.
- Static utility classes and helper classes often have many partitions. A high partition count is not always a problem; it just means the members do not interact.
- Cohesion here is structural: it checks which members use the same state, not whether they belong together *conceptually*.

## Method Complexity

While the other two analyses focus on *types*, **Method Complexity** looks at individual **methods** and answers: **Which methods are the largest and most complicated?**

You can find this analysis in *Analyzers → Method Complexity*. The numbers come from the method bodies and are **collected during import** of a C# solution, then stored with the project. If you import graphs from other sources (like jdeps or plain text), there are no source metrics, and the analyzer will let you know.

The result is a sortable table with one row per method:

| Column     | Meaning                                                      |
| ---------- | ------------------------------------------------------------ |
| Method     | The fully qualified method name.                             |
| Code       | Lines that contain actual code (comment-only and blank lines excluded). |
| Statements | Number of executable statements — the size independent of formatting. |
| Comments   | Comment-only lines, including the `///` documentation comment above. |
| Comment %  | Comments ÷ (Code + Comments) — a rough documentation density. |
| Complexity | Cyclomatic complexity (see below).                           |

### How the numbers are computed

All values are read straight from the method's syntax, no formatting assumptions:

- **Code / Comments** classify each line of the method declaration: a line is *code* if it contains any real token, a *comment* line if it only carries comment trivia, otherwise blank. A line with code and a trailing comment counts as code. Method signature and standalone `{ }` are counted.

- **Statements** count executable statements (wrapping `{ }` blocks are not counted). An expression-bodied method (`=> expr`) counts as one. Independent of how it is laid out.

- **Complexity** is the McCabe cyclomatic complexity: `1` plus one for every decision point — `if`, `while`, `for`, `foreach`, `case`, `catch`, the `?:` operator and the `&&` / `||` / `??` operators. Compound conditions like `if (a && b || c)` are counted as multiple decision points because the short-circuit operators create extra branches. Here, various tools can vary.
  This is roughly the number of independent paths through the method, or the number of test cases needed to cover it.

  Note: In graph theory, the metric is `V(G) = E − N + 2`. This is the number of linearly independent paths through the code. Most tools, however, use the simpler version `V(G) ≈ 1 + D`, where `D` is the number of decision points. Each decision point typically adds exactly one extra edge and one extra node, increasing complexity by 1. The approximation works well for structured code.

### Reading the table

- Sort by **Complexity** (default) to find the branchiest methods — the ones most error-prone and hardest to reason about.  Some consensus guidelines:

  - Complexity ≤ 10: Good/simple

  - 11–20: Moderate

  - 21–50: Complex (should refactor)

  - 50: Very high risk

- Sort by **Code** or **Statements** to find unusually long methods. **Code** can vary depending on formatting, like brace style (K&R vs. Allman) or whether you put one statement per line or several together. So, two methods with the same logic might have different **Code** counts. **Statements** is more consistent because it counts the actual code units, not line breaks, making it a fairer way to compare methods with different layouts.

- **Comment %** is just for context, not a target. If a complex method has almost no comments, it might be under-documented. But a high comment percentage is not always good either.

### Limitations

- Metrics are collected when importing a C# solution and stored with the project. Graphs imported
  from other sources (e.g. jdeps or simple text) have no source metrics.
- The counts are structural, not semantic: they measure size and branching, not whether the logic is
  actually complicated or the comments are useful.
- Complexity counts syntactic decision points; the exact set differs slightly between tools, so absolute
  numbers may not match another analyzer — the *ranking* is what matters.

## System Metrics

While the other analyses give you one row per type or method, **System Metrics** summarizes the whole codebase with a single value for each metric. You can find these in *Analyzers → System Metrics*; the result is a
small table with one row per metric.

| Metric            | Meaning                                                      |
| ----------------- | ------------------------------------------------------------ |
| Propagation cost  | How far a change ripples through the system on average (see below). |
| Cyclicity         | Share of types that sit inside a dependency cycle (see below). |
| Types analyzed    | Number of internal types the metrics are based on (the *N* of the analysis).<br />Class, Interface, Struct, Record, Enum, Delegate |
| Type dependencies | Distinct directed type-to-type dependencies (deduplicated, self edges dropped).<br />Nested types (nested classes/enums) are separate nodes and are counted individually. |

### Propagation cost

Propagation cost answers a straightforward question about the whole code base: **if I change a random type, how much of the rest of the system can the change ripple to on average?**

It is computed on the type-level dependency graph — the same graph the Type Dependencies analyzer uses:
Relationships are lifted to their containing type, deduplicated, and external types are excluded. On that
graph we take the *transitive* reach of every type and average it:

```
propagation cost = (ordered type pairs (A, B), A ≠ B, where A can transitively reach B) / (N · (N − 1))
```

N·(N−1) is simply the maximum number of all possible directed pairs between different types.

So it is the density of the reachability ("who can reach whom") relation, expressed as a percentage:

- **0 %** → fully decoupled: no type can reach any other. Nothing ripples.
- **100 %** → every type can reach every other type. A change can, in principle, touch everything.

This is the classic *propagation cost* of MacCormack, Rusnak and Baldwin, and it is closely related to **blast radius**: blast radius is the transitive reach of a *single* type, propagation cost is the average of that reach over all types, normalized to a percentage.

**How to read it:** Do **not** treat the absolute number as a grade. It depends a lot on the size and
nature of the system, and there is no universal "good" threshold. The real value is in the **trend**: track it over time. If propagation cost goes up with each release, it means changes are getting harder to
contain. If it goes down, the architecture is becoming more decoupled. This metric works well with cycle analysis, since large cycles are a main cause of high propagation costs.

### Cyclicity

Cyclicity answers: **how much of the code base is tangled up in dependency cycles?** It is the share of
types that sit inside a cycle:

```
cyclicity = (types that belong to a strongly connected component of ≥ 2 types) / N
```

It runs on the same type-level graph as propagation cost. We compute the strongly connected components (SCCs) with the same Tarjan algorithm the cycle search uses; a type counts as "cyclic" when it sits in an
SCC of two or more types. A lone type is trivially its own SCC and does not count (self dependencies are
ignored).

- **0 %** → the type graph is acyclic — the ideal.
- **100 %** → every type is part of one big knot.

A **lower value is always better, and 0% is a meaningful goal** at the type level. This is the single-number companion to the *Cycles* view: the Cycle Groups tab shows you *which* cycles exist, while cyclicity tells you *how much* of the system they involve and whether that share is shrinking with each release.

Note this is the system-wide figure. A per-namespace / per-assembly breakdown (which module is the most tangled) is a natural follow-up but has not been computed yet.

## Why not Robert C. Martin's package metrics?

Martin's package (component, here assembly) metrics are frequently cited: **Instability** `I = Ce / (Ca + Ce)`,
**Abstractness** `A = abstract types ÷ all types`, and **Distance from the main sequence**
`D = |A + I − 1|`, where `Ca` / `Ce` are the afferent / efferent coupling of a *package*. I
deliberately **do not** compute them. The reasons:

Many books and articles mention these metrics, but I rarely see anyone actually use them to make improvements. Even worse, abstractness is just a simple count: A is *interfaces + abstract classes ÷ all types*. This confuses the "number of interfaces" with real meaningful abstraction. Creating interfaces just to raise the score does not make a design better, and focusing on the number can lead to unnecessary interfaces. **Distance** is based on A and has the same problem. This criticism is common in practice and matches my own experience: people quote the metric more often than they use it. If you use these metrics, please open an issue and let me know.