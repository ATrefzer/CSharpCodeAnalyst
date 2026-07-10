# C# Code Analyst

[TOC]

**Interactive dependency graph explorer for C# with cycle detection, simulated refactoring, and AI assistance.**

[![GitHub stars](https://img.shields.io/github/stars/ATrefzer/CSharpCodeAnalyst.svg)](https://github.com/ATrefzer/CSharpCodeAnalyst/stargazers)
[![License](https://img.shields.io/github/license/ATrefzer/CSharpCodeAnalyst)](LICENSE)
[![Latest Release](https://img.shields.io/github/v/release/ATrefzer/CSharpCodeAnalyst)](https://github.com/ATrefzer/CSharpCodeAnalyst/releases/latest)

This desktop application helps you **explore, understand, and maintain** large C# codebases — especially when dealing with complex dependencies and architectural issues.

![](Documentation/Images/quick-start.png)

**[Watch the YouTube demo →](https://www.youtube.com/watch?v=o_r1CdQy0tY)** (Cycle analysis)

## Features

- **Full code graph analysis** of your Visual Studio solution
- **Cycle detection & breaking**
- **AI Advisor** – get refactoring suggestions on how to break cycles from a large language model.
- **Interactive Code Explorer** – a visual canvas where you build and explore graphs step by step
- **Simulated Refactoring** – test structural changes without touching your source code
- **Architectural Rules** – define and validate allowed dependencies and metric thresholds.
- **Advanced search & navigation**
- **Export** to PlantUML, DGML, PNG/SVG, and more
- **GIT History Analysis** - perform a hotspot or change coupling analysis on your GIT repository.

## Requirements

- **Windows** (x64)
- **.NET 10 Runtime** (to run the application)
- **.NET SDK or Visual Studio** (provides MSBuild to load your solution)

## Download & Quick Start

1. Download the latest release from the [Releases page](https://github.com/ATrefzer/CSharpCodeAnalyst/releases/latest)
2. Extract the zip and run `CSharpCodeAnalyst.exe`
3. Go to **Home → Import → Import Visual Studio solution**

This builds a complete in-memory graph **model** of your solution (assemblies, namespaces, types, members and relationships).

> **Good to know:** The tool analyzes the code graph, not the file system — the source directory structure is ignored. External assemblies are excluded by default (opt in via settings). See [Limitations](#limitations) for details.

### What you can do from here

- **[Find and break dependency cycles](#find-and-break-dependency-cycles)** — detect strongly connected components and get AI suggestions to break them
- **[Explore your codebase](#explore-your-codebase)** — trace calls, expand inheritance trees, and follow relationships on an interactive canvas
- **[Export your graph](#export-your-graph)** — PlantUML, DGML, PNG/SVG, and more for documentation or further analysis
- **[Validate architectural rules](#validate-architectural-rules)** — define DENY / RESTRICT / ISOLATE rules and check them, in the app or in CI

Independent from the dependency graph tools, you can also analyze a GIT history using the **History Tool**

- **[Analyze a GIT repoository](#analyze-a-git-repository)** 

---

## Find and break dependency cycles

**[Read why you should look for and manage cycles in your code.](Documentation/why-look-for-cycles.md)**

The cycle search always runs on the complete model.

1. Click **Cycles** in the ribbon
2. The *Cycle Groups* tab lists all detected cycles with the involved elements
3. **Right-click** a cycle group → *Show in Code Explorer* to visualize it as a graph. The **Code Explorer** (or canvas) is your interactive working area — a whiteboard where you place only the elements you need right now.
4. Optionally, click **AI Advisor** to get ideas on how to break the cycle

**Note:** The cycle search function finds strongly connected components in the code graph, not the elementary cycles.

A strongly connected component is a sub-graph where a path exists between any two nodes. There may be more than one elementary cycle in the same strongly connected component.

The cycle search result is presented in the **Cycle Groups** Tab.

![](Documentation/Images/cycle-summary.png)

You can analyze a cycle group further in the **Code Explorer.**

![](Documentation/Images/cycle-graph.png)

The Code Explorer now offers a wide range of tools to analyze the cycle. A good strategy is to identify a set of dependencies that seem incorrect to you and focus on them first (see ‘Focus on Incoming Dependencies’, ‘Focus on Outgoing Dependencies’, ‘Focus on Selected Elements’, etc.).

### AI Advisor

Once you have loaded a cycle group into the Code Explorer, the **AI Advisor** button in the toolbar sends the cycle to a configured LLM and asks it for ideas on how to resolve or break down the dependency cycle.

To use this feature, open **Settings** and enter your API endpoint and key. The tool supports any OpenAI-compatible endpoint, including local models (e.g. Ollama) and Anthropic's API.

> **Use with care.** The AI suggestions are generated without any knowledge of your actual business domain, team conventions, or broader system constraints. They may be technically incorrect, impractical, or simply not applicable to your situation.

That said, the feature can be genuinely useful for getting a first set of ideas when you are staring at a complex cycle and don't know where to begin. The AI often recognizes structural patterns — such as hidden abstractions, circular service dependencies, or missing interfaces — that are worth considering. The advice can be saved as a Markdown file for later reference.

![](Documentation/Images/ai-advise.png)

### Simulated refactoring

The refactoring simulation feature is basic but useful. It helps you to explore how changes to the code structure affect cyclic dependencies without modifying the actual source code. A typical scenario involves identifying a large cyclic cluster, making adjustments in the source code, and re-importing the solution - only to find the cycle still unresolved. This process can be repetitive and time-consuming.

To streamline this, the tree view includes a Refactoring context menu. It enables basic refactoring directly on the graph, bypassing the need to edit the source code.

You can explore scenarios such as:

- What happens if you remove a code element?
- What happens if you move a class to another namespace?
- What happens if you cut a dependency between two code elements?

After your modifications, you can rerun the cycle search to observe the impact.

Keep in mind that this is a very basic feature, and you cannot undo modifications to the code graph. So, it's better to save your work before you start.

![](Documentation/Images/refactoring.png)

Context Menu Options:

- **Create code element** – Adds a new element to the model.
- **Delete from model** – Removes the selected element from the model.
- **Set as movement parent** – Sets the current element as the parent for subsequent move operations.
- **Move** – Once a movement parent is set, this option moves the selected element and all its children to the chosen parent.

Additionally in the Code Explorer:

- **Delete edge from model** – Deletes the relationships between two code elements. If the edge is bundled, multiple relationships get deleted.

---

## Explore your codebase

The **Code Explorer** is an interactive canvas where you can explore unfamiliar codebases — trace calls, expand inheritance trees, and follow relationships step by step.

![image-20240731123233438](Documentation/Images/code-explorer.png)

1. Use the **Tree View** or **Advanced Search** tab to search for code elements to add the canvas. The search expression supports `type:class`, `type:method`, `source:intern` and ReSharper-style camel-case search (e.g. `SC` finds `ShoppingCart`).
2. **Right-click** an element on the canvas to explore its relationships with its neighbors.
3. Use the **tool buttons**  in the Code Explorer to perform operations on multiple selected elements.
4. To keep the graph painless, use Hide filters (Ribbon), node collapsing (double-click), and focus on selected elements (tool buttons) or on incoming or outgoing edges (context menu).

### Examples

Here are some general examples of how to use the application to explore a code base.

-  [Essential concepts](Documentation/example-general-concepts.md)
-  [Find the call origins of a method](Documentation/example-find-call-origin.md)
-  [Understand how you could split a large class](Documentation/example-partition-class.md)

### Performance tips

When the graph contains more than ~200 code elements, performance slows down. However, viewing so many elements at once is not helpful. You can collapse and expand container elements by double-clicking them to minimize the number of visible elements. When using the Advanced Search to add multiple code elements, consider adding them in a collapsed state to maintain focus and start with a smaller, faster graph.

---

## Export your graph

You can export your code graph (canvas) in various formats:

- **DGML** for further analysis in Visual Studio
- **PNG** or **SVG** image
- **DSI** if you want to import the graph into a dependency structure matrix tool
- **Plain text**
- **PlantUML**

### PlantUML

When you document code, a UML class diagram is often more helpful than a colored code graph. You can create a UML class diagram from the code elements in the graph. Note that all code elements are included in the diagram, even when collapsed and not visible.

Select "Copy to PlantUML class diagram" from the Export menu.

![](Documentation/Images/export-uml-class-diagram.png)

The PlantUML syntax is copied to the clipboard. You can use any online editor to render it.

![](Documentation/Images/example-uml.png)

---

### Plain Text

That sounds boring, but it’s actually useful. If you want an LLM to carry out a more extensive refactoring, you may find your tokens disappearing – and yet the result may still not be satisfactory. If you provide the LLM with the task and the structural information as a dependency graph in text, the results and token usage improve significantly. The LLM doesn’t even need a description of the graph.

## Validate architectural rules

You can define architectural rules and check if they are violated.
In the ribbon, go to Analyzers and then click "Architectural rules". If a project is loaded, this opens a dialog where you can define the rules.

![](Documentation/Images/rule-configuration.png)

### Supported rules

Dependency restrictions

| Rule     | Meaning                                                      |
| -------- | ------------------------------------------------------------ |
| DENY     | Forbids dependencies from source to target                   |
| RESTRICT | Allows only specified dependencies. RESTRICT rules with the same source are aggregated and the permitted quantity increases. This is unique for the RESTRICT rule. |
| ISOLATE  | Completely isolates the source from external dependencies. Only incoming dependencies are allowed. |
| ALLOW    | Defines an exception. An ALLOW rule never reports violations itself; it suppresses violations matched by other rules. |

Metric-based restrictions. See also [Metrics](Documentation/Metrics.md)

A metric rule limits a measured value instead of a dependency. It is written as `RULE = value`, and `ALLOW` exceptions never affect it. There are two kinds.

**System metric rules** describe the code base as a whole. They take no pattern.

| Rule         | Meaning                                                      |
| ------------ | ------------------------------------------------------------ |
| MAXCYCLICITY | Limits the cyclicity of the whole system. <br />For example, `MAXCYCLICITY = 15` (a percentage between 0 and 100) allows at most 15% of the types to be entangled in cycles. This rule applies to the entire codebase. |

When accepting a baseline a system metric rule gets its threshold raised to the currently measured value, so the rule line is rewritten in place.

**Code element metric rules** limit a value of every code element they match. They may be scoped by a pattern, written as `RULE: Pattern = value`; without a pattern the rule applies to every element in the graph `RULE = value`.

| Rule     | Meaning                                                      |
| -------- | ------------------------------------------------------------ |
| MAXLINES | Limits the size of a single method, in code lines (blank and comment-only lines excluded). <br />For example, <br />`MAXLINES: MyApp.Business.** = 50` reports every method in the business layer that is longer than 50 lines.<br />`MAXLINES = 50` limits all methods in the system to 50 lines. |

An element the rule cannot measure is skipped rather than treated as compliant — an abstract method has no body, so a size limit says nothing about it. Source metrics are collected while importing a solution; if a project has none at all, the rule reports a warning instead of silently passing.

Two metric rules of the same kind never override each other. If `MAXLINES = 50` and `MAXLINES: MyApp.Legacy.** = 200` are both present, a 120-line legacy method violates the first rule — the narrower rule does not grant it an exception.

When accepting a baseline, a code element metric rule remains untouched. Lifting its limit to the worst offender would repeal it for every other element. This is not a baseline but a repeal. This is different from the system metric rules.

Lines of code for methods is just proof of concept I can use when meaningful metrics are collected.

### How patterns work

The source and target side of a rule is a **full path** in the analysis tree: it starts with the **assembly name**, followed by the namespaces and, optionally, a type or member. If the assembly is named like its root namespace, the name appears twice (e.g. `MyApp.MyApp.Business`) — this looks odd at first, but it is the correct path.

You don't have to type these paths by hand: right-click any element in the tree view or graph and choose **"Copy Full Path"** to copy it exactly as the rules expect it.

A pattern can end with a wildcard suffix:

MyApp.MyApp.Business → the element itself

MyApp.MyApp.Business.* → element + direct children

MyApp.MyApp.Business.** → element + all descendants

The part before the wildcard is an **anchor**: it must exactly match the full path of one element (the whole path, not a prefix). The wildcard then expands along the tree — it collects the children of that anchor element, not everything whose name merely starts with the same text. For example, `MyApp.**` matches everything inside the assembly `MyApp`, but nothing in a sibling assembly `MyApp.Utils`, because that assembly is a separate root in the tree and not a child of the anchor.

### Examples

In these examples the assembly is called `MyApp` and contains the namespaces `Business`, `Data`, ... directly — so the paths start with `MyApp.Business`, not with a duplicated name.

```
// Business layer should not access the Data layer directly
DENY: MyApp.Business.** -> MyApp.Data.**

// Controllers may only access Services
RESTRICT: MyApp.Controllers.** -> MyApp.Services.**

// Core components may not depend on UI
DENY: MyApp.Core.** -> MyApp.UI.**

// Keys should be completely isolated, use ALLOW to define exceptions.
ISOLATE: MyApp.Keys.**

// Specific class restrictions
DENY: MyApp.Models.User -> MyApp.Data.Database

// Exceptions: the reporting module may access the Data layer
// even though the Business layer as a whole may not
DENY: MyApp.Business.** -> MyApp.Data.**
ALLOW: MyApp.Business.Reporting.** -> MyApp.Data.**

// At most 15% of all types may sit inside a dependency cycle
MAXCYCLICITY = 15

// No method in the business layer longer than 50 code lines
MAXLINES: MyApp.Business.** = 50
```

The result of the analysis is shown in the table output for analyzers.

If a pattern does not match any code element (for example due to a typo), the rule has no effect. The analysis reports a warning for every such pattern so that silently dead rules are visible.

![](Documentation/Images/rule-result.png)

### Accept a baseline

Introducing rules into an existing code base is the hard part: the first check often reports hundreds of violations, and it is tempting to give up. The **Accept Baseline** button solves this. It becomes available once a validation has found violations. Clicking it freezes the *current* state: every violation is turned into an explicit `ALLOW` exception that is appended to your rules (grouped by the rule it came from). Afterwards the rules are re-validated, so you immediately see a clean result.

From that point on, only *new* violations are reported — the existing ones are accepted as technical debt you can pay down over time. This is what makes the feature practical for real projects rather than only greenfield code: you can adopt an architectural rule today without having to fix everything it flags first.

The exceptions are exact paths down to the member level, so a baseline freezes precisely what exists today. Overloaded methods (which share one path) are all covered by the single exception generated for them.

### Remove unused rules

Over time — after refactorings, or once baselined elements are deleted — rules can end up matching nothing. **Remove unused rules** deletes every rule that currently has no effect (its source or target pattern matches no code element). The cleanup is deliberately conservative: it never removes a rule that still enforces something, so it can never weaken your checks.

### Command-line

To integrate the tool into a build pipeline, you can call it without a user interface. You can find the syntax of the command-line here:

[Command-line arguments](Documentation/command-line-arguments.md)

---

## Metrics

C# Code Analyst can calculate a few but meaningful metrics.

You can read more about the supported metrics here: [Metrics](Documentation/Metrics.md)

All metrics are accessible via the Analyzer Ribbon, and the results are presented in a table on a separate tab

![](Documentation/Images/metrics-example.png)

## Other languages

The tool is written for C#, but you can also import jdeps output for basic visualization of Java code.

```
jdeps.exe -verbose:class <bin-folder1> <bin-folder2>...  >jdeps.txt
```

## Analyze a GIT repository

Years ago, I wrote a repository analyzer based on Adam Tornhill’s book “Your code as a crime scene.” Learn more about the ideas behind this analysis here: https://github.com/ATrefzer/Insight. **Insight** has many more features, but I added the most useful ones to C# Code Analyst. Change coupling is especially interesting since a static analyzer cannot capture it.

Two files are coupled when they often change together. For example, one class encodes a file, and another decodes it. You cannot change one without the other. Such hidden dependencies can be made visible, which fits perfectly into a dependency analyzer tool.

For example, in the first row, 93.1% of commits that contain **Item1** or **Item2**'' committed both items together; therefore, the files may be coupled.

![](Documentation/Images/change-coupling.png)

The second analysis is a hotspot analysis. You can see an example in the screenshot below. The size (LOC) of a file is drawn as the area of a rectangle, and the number of changes is represented as color. The deeper the color, the more often a file was changed over time. Large files that often change are called hotspots and are good candidates to monitor.

![](Documentation/Images/hotspot.png)

Finally, you can analyze a developer's contribution to a file.

That has nothing to do with dependency analysis, but it's helpful if you need to know who to ask for help or which area should be documented when a team member leaves the project.

The developer who contributed most to a file (based on a simple GIT blame) is denoted as the main developer, and the file is colored accordingly. That does not mean this developer has the best knowledge of the file. But it is a reasonable best guess.

![](Documentation/Images/knowledge.png)

## Limitations

Please take note of the following issues:

- The C# Roslyn part only focuses on the most common language constructs. However, even the supported language constructs may be incomplete. C# has a constantly growing language syntax.
- The directory structure of the source code is completely ignored, so keep this in mind when searching for cycles.
- Source locations are not extracted for all dependencies; only those that are easily extractable are included.
- You can include external code by setting the "Include External Code" option. Only type dependencies are collected.
- A method defining a  lambda expression only has "uses" relationships to types and methods inside the lambda.  This is because I cannot track where the lambda is actually called. I think that is a good compromise.
- Primary constructors of records do not create the properties in the code graph.

## Thank you

- The beautiful **images** in the user interface are <a href="https://de.freepik.com/search">Images from juicy_fish on Freepik</a>.
You can find the direct link to the collection here: [Icon-Portfolio des Autors Juicy_fish | Freepik](https://de.freepik.com/autor/juicy-fish/icons)
- The dependency graphs are rendered with **Cytoscape.js**. The default layout is the **fcose**
extension (built on **cose-base** / **layout-base**); additional layered layouts are available via
**dagre** (with **cytoscape.js-dagre**) and **ELK** (**elkjs** with **cytoscape.js-elk**).
Cytoscape.js, fcose, cose-base, layout-base, dagre, cytoscape.js-dagre and cytoscape.js-elk are
MIT-licensed; **elkjs** is licensed under EPL-2.0.
https://github.com/cytoscape/cytoscape.js / https://github.com/iVis-at-Bilkent/cytoscape.js-fcose /
https://github.com/dagrejs/dagre / https://github.com/kieler/elkjs
- SVG export uses the **cytoscape-svg** extension by kinimesi, licensed under GPL-3.0 (same as this project).
https://github.com/kinimesi/cytoscape-svg
- The minimap (bird's-eye overview) in the web graph view uses the **cytoscape.js-navigator** extension, licensed under MIT.
https://github.com/cytoscape/cytoscape.js-navigator
- Drag and drop functionality is provided by the **gong-wpf-dragdrop** library.
Copyright (c) Jan Karger, Steven Kirk and Contributors. Licensed under BSD-3-Clause.
https://github.com/punker76/gong-wpf-dragdrop
- Markdown rendering in the AI Advisor window is powered by **Markdig.Wpf** and **Markdig**.
Copyright (c) Nicolas Musset and Alexandre Mutel. Licensed under BSD-2-Clause.
https://github.com/Kryptos-FR/markdig.wpf / https://github.com/xoofx/markdig

For complete third-party license information, see the [ThirdPartyNotices](ThirdPartyNotices/) folder.

## Contributing

Bug reports, feature ideas, and pull requests are welcome!

## License

This project is licensed under the GPL-3.0 License.

**Note:** Versions prior to v0.9.0 were released under the MIT License.

### What this means for you

- ✅ Use it freely in your projects (even commercial)
- ✅ Modify and improve it
- ⚠️ If you distribute modified versions, share your changes under GPL-3.0
- ℹ️ This is a development tool - it won't affect your analyzed code
