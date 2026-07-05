# Metrics

This document explains the metrics computed by C# Code Analyst. 

Rather than providing a large number of metrics, C# Code Analyst focuses on a small set that answers specific questions.

C# Code Analyst provides three analyses:

- **Type Dependencies** helps you find the most important and riskiest types in a solution.
- **Type Cohesion** helps you find classes that are doing too many unrelated things and may need to be split.
- **Method Complexity** helps you find the largest and most complicated methods.

Together, they help you understand an unfamiliar codebase and identify potential design issues.

The first two work at the type level, the last at the method level. All are accessed from the *Analyzers*
ribbon and their results appear in the Analyzer tab; cohesion additionally drills down into the Partitions tab.

## Type Dependencies

The goal of these metrics is to answer the questions you have when facing an unfamiliar codebase:
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

One note on how the dependencies are counted: If 10 methods in class `A` call 5 methods in class `B` and class `A` accesses one field in class `B`, we count one type dependency `A → B.` **Dependency is a yes/no fact in this context.** For the question "must I understand B in order to understand A?", the answer does not change whether A touches B in five places or fifty. A depends on B, full stop.

Relevant relationship types are `Calls`, `Creates`, `Uses`, `Inherits`, `Implements`, `Overrides`, `UsesAttribute`, `Invokes`. You may have seen a `Handles` relationship in the code graph. This is ignored. It is a special relationship introduced to show which method handles an event. In terms of dependencies, this is the wrong direction. The dependency is recognized when the event is registered, however.

### Fan-in and Fan-out

- **Fan-in** = the number of distinct types that depend on this type. Also known as afferent coupling.
- **Fan-out** = the number of distinct types this type depends on. Also known as efferent coupling.

They answer two different questions, and both are worth reading:

- **High Fan-in** → many things rest on this type. It is *foundational*. Changing it is risky (ripple effect), and it is usually worth understanding early.
- **High Fan-out** → this type knows about many others. It is an *orchestrator* or a potential god-class. It is also a useful starting point, but for a different reason: it shows how behavior is coordinated across the system rather than which types form its foundation..

The extremes are informative too:

- **Fan-in = 0** → nothing depends on this type. It is either an *entry point* (`Main`, a controller, a top-level command/handler) or *dead code*. Both are worth a look.
- **Fan-out = 0** → this type depends on nothing in your solution. A *pure leaf*: a value type, enum or DTO, or a self-contained foundation. These are the stable bottom of the graph.

### Blast radius

**Blast radius** tells you how many other types may be affected when you change a type (transitive). The larger the number, the more carefully you should evaluate changes. Where Fan-in counts only the direct dependents, blast radius follows the incoming edges all the way out.

It answers a blunt, practical question: **how scared should I be to touch this?** A type with a blast radius of 3 is safer to refactor; one with a blast radius of 800 may ripple through most of the codebase.

Blast radius is a **flat count**: every type that can reach you counts as one, regardless of how important it is. That is the difference from Score — Score weights each dependent by its own importance while blast radius does not. A type used by 500 trivial leaves has a large blast radius but may have only a moderate Score. The type itself is never counted in its own blast radius.

Blast radius is always **≥ Fan-in** (the direct dependents are a subset of the transitive ones). When the two are far apart — small Fan-in, large blast radius — the type sits *deep*: few types touch it directly, but those few carry its influence across much of the codebase.

### Score (PageRank)

Fan-in alone has a blind spot: it treats every incoming dependency as equal. A logging utility used by 200 trivial classes gets a huge Fan-in, but it is not an architecturally important type — it is just ubiquitous. Conversely, a core domain type used by only a handful of *central* types can be more important than its raw Fan-in suggests.

**Score** addresses this limitation by measuring *transitive* importance: a type is important when **important types depend on it**, not merely when many types do. This is the PageRank algorithm, the same idea Google originally used to rank web pages.

(!) In everyday terms: on the web, PageRank estimates how likely you are to reach a page by following links; here it estimates how much of the rest of the codebase ultimately rests on a type — directly, and through the other types that rest on it. A high Score means a lot of your code leans on this type, so it is both what you most need to understand and what is riskiest to change.

Score is the PageRank value normalized so the **average type scores 1.0**:

- **Score = 1.0** → an average type.
- **Score = 5.0** → five times more central than the average type.
- **Score < 1.0** → below-average centrality (most leaf types).

### Reading the numbers

1. Sort by **Score** (default) to find the types the rest of the code leans on most. These carry the most weight — the highest payoff to understand and the highest risk to change. That is not automatically where you start reading: it depends on your approach. Working bottom-up, these foundational types are the system's vocabulary and a natural starting point; working top-down, you may prefer to start with the orchestrators below and drill down into them.
2. Sort by **Fan-out** to find the major orchestrators — these often provide a good overview of how the system's behavior is coordinated.
3. Sort by **Blast radius** before a refactoring — it tells you how far the ripples of a change to a type will reach.
4. Watch for the disagreement between Fan-in and Score: a type with high Fan-in but modest Score is a widely-used utility (a logger, an extension-method holder); a type with modest Fan-in but high Score is a genuine architectural core. That gap is often the most informative signal in the table.
5. **High Score together with high Fan-out** is the most dangerous combination: the type is both foundational (much rests on it) and an orchestrator (it knows everyone). Such types are often god classes you cannot change without touching a lot — the first candidate to break apart.

### How it is computed

PageRank is computed by power iteration on the type-level graph:

```
PR(v) = (1 - d) / N  +  d · Σ  PR(u) / outdegree(u)
                          u → v
```

- `N` = number of types, `d` = damping factor (0.85).
- Edges are **not reversed.** Rank flows along `A → B` edges toward the depended-upon type `B`, so foundational types (base classes, interfaces, core services) accumulate rank and rise to the top.

The raw PageRank values form a **probability distribution**: they sum to 1 over all types, so on a large solution each value is tiny. The **Score** shown in the table is that value multiplied by `N` (the number of types), which rescales the average to exactly 1.0:

```
average Score = (1 / N) · N = 1
```

### Limitations

- Metrics are structural only. They say nothing about code quality, correctness, or how hard a type is to read internally — only about its position in the dependency graph.
- Dependencies are counted as yes/no; the strength or frequency of a coupling is not modeled.
- External types are excluded, so a type whose real importance comes from being called by framework callbacks (e.g., a controller invoked only by ASP.NET) may rank lower than its runtime role. However, when counting externals at the type level, objects, strings, etc. would dominate immediately.


## Type Cohesion

Where *Type Dependencies* looks at a class from the outside, **Type Cohesion** looks *inside* a class
and answers a different question: **does this class contain multiple independent responsibilities — and if so, how many?**

Available via *Analyzers → Type Cohesion*. The result is a sortable table listing only the classes
that are split candidates:

| Column     | Meaning                                                                    |
| ---------- | -------------------------------------------------------------------------- |
| Class      | The fully qualified class name.                                            |
| Partitions | Into how many independent groups the class's members fall.                 |
| Members    | Number of direct members — size / priority context.                       |
| Largest %  | Share of the members that sit in the biggest partition (see below).        |

Double-click a row to open its partitions in the *Partitions* tab.

### What a partition is

The members of a class (methods, fields, properties, …) are **connected** when one calls the other, or they access the same field. Members belong to the same *partition* when they work together. If two groups of members never interact, they end up in different *partitions*.

Multiple partitions often indicate that a class contains multiple responsibilities..

- **1 partition** → fully cohesive: everything is interconnected. (These classes are *not* listed.)
- **N ≥ 2 partitions** → the class is really N separable units. It could be split into N smaller,
  more focused classes.

This is the connected-components view of cohesion (LCOM4).

Only **classes** are analyzed (not structs, records or interfaces). Pure data holders are skipped: a class with fewer than two methods has too little behavior for cohesion to mean anything, and would otherwise show up as maximally "incohesive" (each field its own partition).

### Base classes are folded in

Methods of a class may be linked together through base-class members. 

Therefore, base-class members are pulled in as **connectors**: they link the class's own members that interact through inherited state or behavior, but are then **projected out** of the reported partitions — because a split concerns the members *this* class actually owns.  External base classes are ignored.

### Reading the numbers

**Partitions** tells you *that* a class falls apart; **Largest %** tells you *whether it is worth splitting*. Two classes with the same partition count can be very different:

| Class | Partitions | Members | Largest % | Reading                                                      |
| ----- | ---------- | ------- | --------- | ------------------------------------------------------------ |
| X     | 2          | 19      | 53 %      | 10 vs 9 — a class that contains two distinct responsibilities. |
| Y     | 2          | 19      | 95 %      | 18 vs 1 — one cohesive blob plus a stray helper.             |

**Largest %** is the size of the biggest partition divided by all partitioned members:

- Near **100 %** → one dominant group plus a few strays. The "split" is trivial (shed one method). Low priority.
- Near **1 / Partitions** (evenly divided) → the class breaks into balanced, separate responsibilities. A real refactoring candidate. High priority.

Read the three columns together:

- **Many Partitions + many Members + low Largest %** → the worst offenders: a big class that genuinely breaks into several balanced, separate parts. Sort by **Largest % ascending** to bring these to the top, and use **Members** to pick the higher-priority candidates among them.
- **High Partitions + high Largest %** → the opposite shape: one solid core and many tiny, unrelated helpers. You can peel these off one at a time rather than doing a big split.
- **Few Members with ≥ 2 partitions** → low stakes. Technically incohesive, but too small to be worth acting on.

### Drilling into the partitions

Double-clicking a row (or right-click → *Show partitions*) opens the concrete groups in the
*Partitions* tab, so you immediately see *which* members would go where.

### Limitations

- Only in-solution base classes are folded in; members inherited from framework types are not visible to the analysis.
- Static utility classes and miscellaneous helper classes legitimately show many partitions — a high partition count is not automatically a defect, only a signal that the members do not interact.
- Cohesion is structural: it sees which members touch the same state, not whether they belong together *conceptually*.

## Method Complexity

Where the other two analyses look at *types*, **Method Complexity** zooms in on individual **methods**
and answers: **which methods are the largest and the most complicated**

Available via *Analyzers → Method Complexity*. Because the numbers come from the method bodies, they
are **collected during import** of a C# solution and stored with the project. Graphs imported from
other sources (e.g. jdeps or plain text) have no source metrics, and the analyzer tells you so.

The result is a sortable table with one row per method:

| Column     | Meaning                                                                      |
| ---------- | ---------------------------------------------------------------------------- |
| Method     | The fully qualified method name.                                            |
| Code       | Lines that contain actual code (comment-only and blank lines excluded).      |
| Statements | Number of executable statements — the size independent of formatting.        |
| Comments   | Comment-only lines, including the `///` documentation comment above.          |
| Comment %  | Comments ÷ (Code + Comments) — a rough documentation density.                |
| Complexity | Cyclomatic complexity (see below).                                          |

### How the numbers are computed

All values are read straight from the method's syntax, no formatting assumptions:

- **Code / Comments** classify each line of the method declaration: a line is *code* if it contains any real token, a *comment* line if it only carries comment trivia, otherwise blank. A line with code and a trailing comment counts as code. Method signature and standalone `{ }` are counted.

- **Statements** count executable statements (wrapping `{ }` blocks are not counted). An expression-bodied method (`=> expr`) counts as one. Independent of how it is laid out.

- **Complexity** is the McCabe cyclomatic complexity: `1` plus one for every decision point — `if`, `while`, `for`, `foreach`, `case`, `catch`, the `?:` operator and the `&&` / `||` / `??` operators. Compound conditions like `if (a && b || c)` are counted as multiple decision points because the short-circuit operators create extra branches. Here, various tools can vary.
  This is roughly the number of independent paths through the method, i.e., the number of test cases required to cover it.
  
  Note: In graph theory, the metric is `V(G) = E − N + 2`. This is the number of linearly independent paths through the code. Most tools, however, use the simpler version `V(G) ≈ 1 + D`, where `D` is the number of decision points. Each decision point typically adds exactly one extra edge and one extra node, increasing complexity by 1. The approximation works well for structured code.

### Reading the table

- Sort by **Complexity** (default) to find the branchiest methods — the ones most error-prone and hardest to reason about.  Some consensus guidelines:
  
  - Complexity ≤ 10: Good/simple
  
  - 11–20: Moderate
  
  - 21–50: Complex (should refactor)
  
  - 50: Very high risk
  
- Sort by **Code** or **Statements** to find methods that are unusually long. **Code** still varies with how the method is laid out — brace style (K&R vs. Allman), one statement per line versus several packed together — so two methods with identical logic can end up with different **Code** counts. **Statements** does not: it counts the syntactic units directly, independent of line breaks, which makes it the fairer size when comparing methods written in different layout styles.

- **Comment %** is context, not a target: near-zero on a complex method may mean it is under-documented. A high value is not automatically good.

### Limitations

- Metrics are collected when importing a C# solution and stored with the project. Graphs imported
  from other sources (e.g. jdeps or plain text) have no source metrics.
- The counts are structural, not semantic: they measure size and branching, not whether the logic is
  actually complicated or the comments are useful.
- Complexity counts syntactic decision points; the exact set differs slightly between tools, so absolute
  numbers may not match another analyzer — the *ranking* is what matters.

## Why not Robert C. Martin's package metrics?

Martin's package (component, here assembly) metrics are frequently cited: **Instability** `I = Ce / (Ca + Ce)`,
**Abstractness** `A = abstract types ÷ all types`, and **Distance from the main sequence**
`D = |A + I − 1|`, where `Ca` / `Ce` are the afferent / efferent coupling of a *package*. I
deliberately **do not** compute them. The reasons:

While many books and articles mention these metrics, I can barely find anyone who actually uses them to improve anything. Even worse, abstractness is a shallow syntactic count. A is just *interfaces + abstract classes ÷ all types*. It confuses "number of interfaces" with genuine abstraction: extracting an interface to raise the score does not make a design more abstract, and optimizing for the number invites interface-for-its-own-sake churn. **Distance** is built on A and inherits this weakness. This is a common criticism in practice, and matches our own experience: the metric is quoted far more often than it is acted upon. I'm curious. If you use these metrics, please create an issue and let me know.
