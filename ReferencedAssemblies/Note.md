This folder contains the MSAGL assemblies.

Instead of the NUGET package, a modified version built from the fork https://github.com/ATrefzer/automatic-graph-layout (branch: `csharp-code-analyst-changes`) is used.

**Changes**

- Hide the collapse button for subgraphs by default and use a context menu for collapse/expand. The original repository had bugs, which are now resolved. However, this approach has the added benefit of enabling lazy loading, allowing handling of very large graphs that are initially collapsed.

