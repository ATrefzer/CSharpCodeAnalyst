This folder contains the MSAGL assemblies.

Instead of the NUGET package, a modified version built from the fork https://github.com/ATrefzer/automatic-graph-layout (branch: csharp-code-analyst-changes) is used.

**Changes**

- Hide the collapse button for subgraphs by default. I use now my own mechanism to collapse/expand. The original repository had bugs, making it unusable. Instead, I handle collapse now by rendering a completely new graph. This approach has the added benefit of enabling lazy loading, allowing handling of huge graphs that are initially collapsed.
- Performance optimized Canvas
- Added possibility to use font styles for the labels and dotted lines for the nodes.



**Note**

Compile Samples\WpfApplicationSample to get all assemblies at once.
