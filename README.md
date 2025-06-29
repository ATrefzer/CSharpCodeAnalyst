# C# Code Analyst

This application helps you to explore, understand, and maintain C# code.

Here is a [presentation on YouTube](https://www.youtube.com/watch?v=o_r1CdQy0tY) on using the application to analyze cyclic dependencies.

Note: MSBuild must be installed on your computer for the application to work.

## Exploring your codebase

![image-20240731123233438](Images/code-explorer.png)

- Use the tree view to add code elements to the canvas.
- You can explore relationships between code elements using the context menu on a code element.
- Use the context menu to automatically connect all code elements in the space around the graph.
- Pressing **Control + Left Mouse Click** will keep the quick help for the clicked element active.
- To pan, press **Shift + Left Mouse Button** and move the mouse in empty canvas space.

- You can export graphs to DGML for further analysis in Visual Studio.

## Find and visualize cycles in your codebase

**Note:  This function finds strongly connected components in the code graph, not the elementary cycles. **



![](Images/cycle-summary.png)

A strongly connected component is a sub-graph where a path exists between any two nodes. There may be more than one elementary cycle in the same strongly connected component.

Use the context menu to copy the related code elements to the explorer graph for further investigation.

![](Images/cycle-graph.png)

### **Why Look for Cycles?**

More than 40 years ago, in his often-cited paper ["Designing software for ease of extension and contraction"](https://courses.cs.washington.edu/courses/cse503/08wi/parnas-1979.pdf) David Parnas suggested organizing software hierarchically, keeping the modules "loop-free." Similarly, Robert C. Martin's Acyclic Dependency Principle pushes in the same direction.

This idea of having cycle-free modules is quite intuitive. Let's look at an example outside the software world: Imagine a project plan with two tasks, A and B, depending on each other, forming a cycle. How would you tackle these tasks? You'd have to do them together as a whole. It's similar in software. If there are cycles in the area you want to change, you might end up reading and understanding all the classes involved in the cycle. Changes can easily have side effects in unexpected areas. Consequently, a software system with circular dependencies is more difficult to maintain.

The preference for hierarchical structures in software isn't arbitrary. It's deeply rooted in how our brains process information:

1. Research in cognitive psychology has consistently shown that the human brain understands and processes hierarchical structures more easily than non-hierarchical or cyclic ones.
2. We naturally organize our knowledge hierarchically, which makes hierarchical code structures more intuitive to understand and remember.

Therefore, I see this advice as a timeless principle. While studies on how we learn and understand things may be old, they will never be outdated. The main tool we use to write software, our brain, will be the same tomorrow.

There are other attributes associated with hierarchical and cycle-free systems like testability, maintainability, etc. For me, the matter of understanding the system is the most important. I doubt that you can have maintainability in a hard-to-understand codebase.

**C# Code Analyst** helps you identify cycles in your code, offering a higher-level perspective on your code structure. By using this tool, you can:

- Gain insights into your code's organization that might not be apparent when working at the detailed level.
- Identify opportunities to refactor and improve your code's structure.
- Enhance the overall readability and maintainability of your codebase.

Remember, the goal isn't to eliminate every cycle but to be aware of your code's structure and make informed decisions about its organization. Some cycles may be intended, even some design patterns use them. By focusing on readability, you're investing in code that's not just functional, but also easier to understand, maintain, and evolve.

**In general, I think it's a good guideline to keep your software system free of cycles at the namespace level.**

## Limitations

Please take note of the following issues:

- The graph layout package does not support multiple edges between the same code elements when sub-graphs are used; the edges are combined in this case.
- The directory structure of the source code is completely ignored, so keep this in mind when searching for cycles.
- The tree view search may be very slow, depending on the result.
- Source locations are not extracted for all dependencies, only the ones that are easy to extract.
- External code is ignored.
- The C# Roslyn part only focuses on the most common language constructs. However, even the supported language constructs may be incomplete. For any known unsupported syntax, refer to [Documentation\Uncovered C# Syntax.md](Documentation/Uncovered%20C#%20Syntax.md")

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
