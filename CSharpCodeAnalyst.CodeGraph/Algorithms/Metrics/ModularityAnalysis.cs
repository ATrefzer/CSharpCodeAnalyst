namespace CSharpCodeAnalyst.CodeGraph.Algorithms.Metrics;

// This is just a (failed) experiment, but I keep it because it taught me some interesting things.
// I calculate the entropy of the whole system on the type leve (ignoring any modules). Then again but now considering modules.
// My hope was that this could describe how tangled a system is. But that is not true. It is the percentage
// of bits needed to describe the system given the modules compared to the whole system. However, this number has no absolute meaning.
// You cannot use it to compare or evaluate anything.

/// <summary>
///     Measures how well a given module partition explains the dependency structure, using the
///     Minimum Description Length (MDL) idea: how many bits does it take to write down the complete
///     "who depends on whom" table - and how much shorter does that get when the module boundaries
///     may be used as a hint? The answer is returned as <em>tangledness</em>: the share of the bits
///     that is still needed despite the hint.
///     <para>
///         <b>The four terms used throughout this class</b>, each meaning exactly one thing:
///         <list type="bullet">
///             <item>
///                 <b>slot</b> - one cell of the N x N type matrix, i.e. one <em>possible</em> directed
///                 edge between two different types. There are N * (N-1) of them. A slot is either
///                 filled (the dependency exists) or empty.
///             </item>
///             <item>
///                 <b>density</b> - filled slots / slots, for some region of the matrix. A plain ratio
///                 in [0,1] and a property of the data - not a bit count.
///             </item>
///             <item>
///                 <b>H(density)</b> - binary entropy: the <em>bits per slot</em> needed to write a
///                 region of that density down. H(0) = H(1) = 0 - an always-empty or always-full region
///                 is free, because the reader can derive every slot without being told. H(0.5) = 1 bit
///                 - a coin flip, every single slot has to be spelled out.
///             </item>
///             <item>
///                 <b>L</b> - description length: the <em>total bits</em> for a region, i.e.
///                 <c>slots * H(density)</c>. The letter is from "Length", as in MDL.
///             </item>
///         </list>
///         The chain is therefore: density (a ratio) --H--&gt; bits per slot --* slots--&gt; L (total bits).
///     </para>
///     <para>
///         Two descriptions of the <em>same</em> data are compared. Both cover the <b>whole</b> matrix -
///         every slot, the ones inside modules just as much as the ones across them. They differ only
///         in how many densities they are allowed to use:
///         <list type="bullet">
///             <item>
///                 <c>L_baseline</c> - one single density for the entire matrix:
///                 <c>totalSlots * H(globalDensity)</c>.
///             </item>
///             <item>
///                 <c>L_block</c> - the matrix is cut along the module boundaries into K x K blocks.
///                 Block (r,s) holds all slots from a type in module r to a type in module s; the
///                 diagonal blocks (r,r) are a module's internals. Every block gets its own density:
///                 the sum of <c>slots * H(blockDensity)</c> over all blocks.
///             </item>
///         </list>
///     </para>
///     <para>
///         A partition that matches reality pushes every block towards empty or full - both cheap - so
///         <c>L_block</c> falls well below <c>L_baseline</c>. A partition that the dependencies ignore
///         leaves every block looking like the global average, and nothing is saved.
///     </para>
///     This is the information-theoretic relative of Newman's modularity Q, and the same family as
///     Infomap (Rosvall &amp; Bergstrom 2008) and stochastic block model inference - all textbook and
///     dependency-free (only counting and log2).
/// </summary>
public static class ModularityAnalysis
{
    /// <summary>
    ///     Tangledness in [0,1]: <c>L_block / L_baseline</c> - the share of the bits still needed to
    ///     describe the dependency matrix even when the module boundaries may be used as a hint.
    ///     0 = the modules explain the dependencies completely (every block is empty or full),
    ///     1 = the modules explain nothing (every block looks like the global average).
    ///     <para>
    ///         The value can never leave [0,1]: cutting a region into blocks and giving each its own
    ///         density can never need <em>more</em> bits than one density for everything, because the
    ///         binary entropy is concave (Jensen). Hence <c>L_block &lt;= L_baseline</c>.
    ///     </para>
    ///     <para>
    ///         Careful: the value reacts to the <em>granularity</em> of the partition, not only to the
    ///         code. The finer the modules, the more cross blocks are trivially empty in a sparse graph,
    ///         and the lower the result. The two degenerate ends show it: one module per type yields 0
    ///         (every block is a single slot, which is always empty or full - "perfect" without any
    ///         insight), while a single module holding everything yields exactly 1 (that one block IS
    ///         the whole matrix, so the block model collapses into the baseline). Only compare runs
    ///         that use the same partition scheme.
    ///     </para>
    /// </summary>
    /// <param name="graph">The type-level dependency graph.</param>
    /// <param name="modules">
    ///     The partition: maps every vertex of <paramref name="graph" /> to the id of the module that
    ///     contains it. Every vertex must have an entry.
    /// </param>
    public static double CalculateTangledness(TypeGraph graph, IReadOnlyDictionary<string, string> modules)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(modules);

        var n = graph.VertexCount;

        // One slot = one possible directed edge between two different types. The diagonal (a type
        // depending on itself) is not part of the matrix, hence N * (N-1) rather than N * N.
        var totalSlots = (long)n * (n - 1);
        if (totalSlots == 0 || graph.EdgeCount == 0)
        {
            // Nothing is coupled, so nothing can be tangled. Matches Cyclicity / PropagationCost,
            // which are 0 for the same graph.
            return 0.0;
        }

        // L_baseline: the whole matrix described with one single density.
        var baselineBits = totalSlots * BinaryEntropy((double)graph.EdgeCount / totalSlots);
        if (baselineBits <= 0.0)
        {
            // The "no edges" case already returned above, so the density is > 0 and H can only be 0
            // because the density is exactly 1: a complete graph, every possible dependency exists.
            // Then every block is full as well, L_block is 0 too, and the ratio would be 0/0. The
            // limit is 1, not 0: when every block matches the global density, L_block == L_baseline
            // by definition - no module boundary saves a single bit, which is maximal tangledness.
            // Cyclicity and PropagationCost are also 1 for this graph.
            // (The <= instead of == only guards against log2 rounding noise; entropy is never negative.)
            return 1.0;
        }

        var sizes = ModuleSizes(graph, modules);
        var filled = FilledPerBlock(graph, modules);

        // L_block: the same matrix, but every block (r,s) described with its own density. The loop
        // covers all K*K blocks - the cross blocks and the diagonal ones (a module's internals).
        var blockBits = 0.0;
        foreach (var (r, sizeR) in sizes)
        {
            foreach (var (s, sizeS) in sizes)
            {
                // Inside one module a type cannot depend on itself, hence the -1. Across two distinct
                // modules no such correction is needed - they share no type.
                var slots = r == s ? (long)sizeR * (sizeR - 1) : (long)sizeR * sizeS;
                if (slots == 0)
                {
                    continue; // A module holding a single type has no internal slots.
                }

                var count = filled.GetValueOrDefault((r, s));
                blockBits += slots * BinaryEntropy((double)count / slots);
            }
        }

        // Tangledness: the share of the baseline bits that survives the module hint.
        return blockBits / baselineBits;
    }

    /// <summary>Number of types per module.</summary>
    private static Dictionary<string, int> ModuleSizes(TypeGraph graph, IReadOnlyDictionary<string, string> modules)
    {
        var sizes = new Dictionary<string, int>();
        foreach (var vertex in graph.Vertices)
        {
            var module = modules[vertex];
            sizes[module] = sizes.GetValueOrDefault(module) + 1;
        }

        return sizes;
    }

    /// <summary>
    ///     How many of a block's slots are filled: the number of existing edges that run from a type in
    ///     the source module to a type in the target module. Picture the aggregated K x K module matrix
    ///     (not the N x N type matrix) - this is the value of its cell (source, target).
    ///     <para>
    ///         Directed: (r,s) and (s,r) are counted separately, so a one-way dependency between two
    ///         modules stays distinguishable from a mutual one.
    ///     </para>
    /// </summary>
    private static Dictionary<(string SourceModule, string TargetModule), int> FilledPerBlock(
        TypeGraph graph, IReadOnlyDictionary<string, string> modules)
    {
        var filled = new Dictionary<(string SourceModule, string TargetModule), int>();
        foreach (var source in graph.Vertices)
        {
            var r = modules[source];
            foreach (var target in graph.Out[source])
            {
                var key = (r, modules[target]);
                filled[key] = filled.GetValueOrDefault(key) + 1;
            }
        }

        return filled;
    }

    /// <summary>
    ///     Binary entropy: the average number of <em>bits per slot</em> needed to describe a region
    ///     whose density is <paramref name="p" />. An always-empty (p=0) or always-full (p=1) region is
    ///     free - the reader can derive every slot without being told. A half-filled one (p=0.5) costs a
    ///     full bit per slot, because every slot is an independent coin flip.
    /// </summary>
    private static double BinaryEntropy(double p)
    {
        if (p <= 0.0 || p >= 1.0)
        {
            return 0.0;
        }

        return -p * Math.Log2(p) - (1 - p) * Math.Log2(1 - p);
    }
}
