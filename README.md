# C# Code Analyst



![](assets/traffic-barrier.png) **Under development**

This application helps you to explore, understand, and maintain C# code.

## Exploring your codebase

![image-20240731123233438](assets/code-explorer.png)

- Use the tree view to add code elements to the canvas.
- You can explore relationships between code elements using the context menu on a code element.
- Use the context menu to automatically connect all code elements in the space around the graph.
- Pressing **Control + Left Mouse Click** will keep the quick help for the clicked element active.
- To pan, press **Shift + Left Mouse Button** and move the mouse in empty canvas space.

- You can export graphs to DGML for further analysis in Visual Studio.

## Find and visualize cycles in your codebase

**Note:  This function finds strongly connected components in the code graph, not the elementary cycles. **



![](assets/cycle-summary.png)

A strongly connected component is a sub-graph where a path exists between any two nodes. There may be more than one elementary cycle in the same strongly connected component.

Use the context menu to copy the related code elements to the explorer graph for further investigation.

## Dependency Structure Matrix (DSM)

**Not tested.**

A **DSM** (Dependency Structure Matrix) displays all the dependencies in your codebase in a condensed matrix. It may take some time to become familiar with it. For further explanation, you can visit https://dsmsuite.github.io/.

The **DSM Suite Viewer** is integrated into C# Code Analyst

<img src="assets/dsm-suite.png"  />

The viewer can be found in this repository:
https://github.com/ernstaii/dsmsuite.sourcecode,
which was forked from
https://github.com/dsmsuite/dsmsuite.sourcecode

## Limitations

Please take note of the following issues:

- There is a bug in the graph layout package that may cause the application to crash when you collapse sub-graphs.
- The graph package does not support multiple edges between the same code elements when sub-graphs are used; the edges are combined in this case.
- Collapsing sub-graphs does not remap the incoming dependencies to the collapsed sub-graph. To observe this, export the graph to DGML and open it in Visual Studio.
- The directory structure of the source code is completely ignored, so keep this in mind when searching for cycles.
- The tree view search may be very slow, depending on the result.
- Source locations are not extracted for all dependencies, only the ones that are easy to extract.
- External code is ignored.

- The C# Roslyn part only focuses on the most common language constructs. However, even the supported language constructs may be incomplete. For any known unsupported syntax, refer to [Documentation\Uncovered C# Syntax.md](Documentation/Uncovered C# Syntax.md)

## Thank you

- The beautiful **images** in the user interface are <a href="https://de.freepik.com/search">Images from juicy_fish on Freepik</a>
  You can find the direct link to the collection here: [Icon-Portfolio des Autors Juicy_fish | Freepik](https://de.freepik.com/autor/juicy-fish/icons)

- The dependency graphs are created using the **"Automatic Graph Layout" package"**. You can find more information about it at:
  https://github.com/microsoft/automatic-graph-layout

- The **DSM viewer** is part of the **DSM Suite** project. You can access it at
  https://github.com/ernstaii/dsmsuite.sourcecode
  https://github.com/dsmsuite/dsmsuite.sourcecode

## Supporting this project

If you find any uncovered cases or bugs, please create an issue to support this project.