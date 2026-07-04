# Why look for cycles?

More than 40 years ago, in his often-cited paper ["Designing software for ease of extension and contraction"](https://courses.cs.washington.edu/courses/cse503/08wi/parnas-1979.pdf) David Parnas suggested organizing software hierarchically, keeping the modules "loop-free." Similarly, Robert C. Martin's Acyclic Dependency Principle pushes in the same direction.

This idea of having cycle-free modules is quite intuitive. Let's look at an example outside the software world: Imagine a project plan with two tasks, A and B, that depend on each other, forming a cycle. How would you tackle these tasks? You'd have to do them together as a whole. It's similar in software. If there are cycles in the area you want to change, you might end up reading and understanding all the classes involved in the cycle. Changes can have unexpected side effects. Consequently, a software system with circular dependencies is more challenging to maintain.

The preference for hierarchical structures in software isn't arbitrary. It's deeply rooted in how our brains process information:

1. Research in cognitive psychology has consistently shown that the human brain understands and processes hierarchical structures more easily than non-hierarchical or cyclic ones.
2. We naturally organize our knowledge hierarchically, which makes hierarchical code structures more intuitive to understand and remember.

Therefore, this advice is a timeless principle. While studies on how we learn and understand things may be old, they will never be outdated. The primary tool we use to write software, our brain, will be the same tomorrow.

There are other attributes associated with hierarchical and cycle-free systems, such as testability and maintainability. To me, understanding the system is the most important aspect. I doubt that you can have maintainability in a hard-to-understand codebase.

**C# Code Analyst** helps you identify cycles in your code, offering a higher-level perspective on your code structure. By using this tool, you can:

- Gain insights into your code's organization that might not be apparent when working at the detailed level.
- Identify opportunities to refactor and improve the structure of your code.
- Enhance the overall readability and maintainability of your codebase to improve its quality.

Remember, the goal isn't to eliminate every cycle but to be aware of your code's structure and make informed decisions about its organization. Some cycles may be intentional, and even some design patterns utilize them. By focusing on readability, you're investing in code that's not just functional, but also easier to understand, maintain, and evolve.

**In general, it's a good guideline to keep your software system free of cycles at the namespace level.**