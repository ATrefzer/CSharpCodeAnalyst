namespace CSharpCodeAnalyst.History.Analyzer;

/// <summary>
///     Self-contained hierarchical tree node for the hotspot analysis. Deliberately independent from
///     CSharpCodeAnalyst.TreeMap.Data.HierarchicalData (which lives in a WPF-only assembly) so this
///     library stays free of any UI dependency. If a second hierarchical analysis needs the same
///     tree-building machinery, extract the shared parts into a base type then - not before.
/// </summary>
public sealed class HotspotNode
{
    private const string PathSeparator = "/";

    private readonly List<HotspotNode> _children = [];

    public HotspotNode(string name)
    {
        Name = name;
        Description = name;
        AreaMetric = double.NaN;
    }

    /// <summary>Leaf node constructor - must provide an area metric.</summary>
    public HotspotNode(string name, double areaMetric, double weightMetric, bool weightIsAlreadyNormalized = false)
    {
        Name = name;
        Description = name;
        AreaMetric = areaMetric;
        WeightMetric = weightMetric;

        if (weightIsAlreadyNormalized)
        {
            NormalizedWeightMetric = weightMetric;
            if (weightMetric is < 0.0 or > 1.0)
            {
                throw new ArgumentException("Normalized weight not in range [0,1]");
            }
        }
    }

    public string Name { get; }
    public string Description { get; set; }
    public string? ColorKey { get; set; }
    public object? Tag { get; set; }
    public HotspotNode? Parent { get; set; }
    public IReadOnlyList<HotspotNode> Children => _children;
    public bool IsLeafNode => _children.Count == 0;

    public double AreaMetric { get; }
    public double AreaMetricSum { get; private set; }
    public double WeightMetric { get; }
    public double NormalizedWeightMetric { get; private set; }

    public static HotspotNode NoData()
    {
        return new HotspotNode("No Data", 1, 0);
    }

    public void AddChild(HotspotNode child)
    {
        _children.Add(child);
        child.Parent = this;
    }

    public string GetPathToRoot()
    {
        var path = new List<string>();
        var current = this;
        while (current != null)
        {
            path.Add(current.Name);
            current = current.Parent;
        }

        path.Reverse();

        // An artificial root node with name "" automatically makes the path start with a /, but we
        // want that in all cases.
        var description = string.Join(PathSeparator, path);
        if (!description.StartsWith(PathSeparator, StringComparison.InvariantCulture))
        {
            description = PathSeparator + description;
        }

        return description;
    }

    /// <summary>
    ///     Removes leaf nodes without an area, recursively - collapsing a branch can turn its parent
    ///     into a new, now-empty leaf, which is removed too. Throws if nothing is left.
    /// </summary>
    public void RemoveLeafNodesWithoutArea()
    {
        RemoveLeafNodesWithoutArea(this);

        if (IsLeafNode && double.IsNaN(AreaMetric))
        {
            throw new InvalidOperationException("Hierarchical data is not valid. Singular root node does not have an area.");
        }
    }

    /// <summary>
    ///     Updates the area metrics from the leaves up to the root, and sorts children by descending
    ///     area sum (the tree-map algorithm works best with the largest items first).
    /// </summary>
    public void SumAreaMetrics()
    {
        if (IsLeafNode)
        {
            if (double.IsNaN(AreaMetric))
            {
                throw new ArgumentException("Area metric is unknown for leaf node");
            }

            if (Math.Abs(AreaMetric) < double.Epsilon)
            {
                throw new ArgumentException("Area metric is 0. This is not allowed.");
            }

            AreaMetricSum = AreaMetric;
            return;
        }

        var sum = 0.0;
        foreach (var child in _children)
        {
            child.SumAreaMetrics();
            sum += child.AreaMetricSum;
        }

        AreaMetricSum = sum;
        _children.Sort((a, b) => b.AreaMetricSum.CompareTo(a.AreaMetricSum));
    }

    /// <summary>
    ///     The weight metric is normalized only across the leaf nodes, using a rank-based
    ///     (percentile) mapping instead of min-max: commit counts are heavily skewed, so with
    ///     min-max a single outlier gets all the color and almost every other leaf is compressed
    ///     into the bottom of the scale. With percentiles the median leaf sits in the middle of
    ///     the color ramp. Ties share the same percentile (average rank), so equal weights always
    ///     get equal colors - including the all-equal case, which min-max cannot handle at all
    ///     (division by zero).
    /// </summary>
    public void NormalizeWeightMetrics()
    {
        var leaves = new List<HotspotNode>();
        CollectLeaves(leaves);

        if (leaves.Count == 0)
        {
            return;
        }

        if (leaves.Count == 1)
        {
            leaves[0].NormalizedWeightMetric = 1.0;
            return;
        }

        // Tuning point. The rank-based mapping below encodes only the ORDER of the leaves -
        // distances are lost (the 2nd hottest file looks almost as red as the hottest, even if
        // it has a fraction of the commits). If the proportions matter more than the ordering,
        // replace everything below with a dampened min-max mapping instead:
        //
        //     var min = leaves.Min(l => l.WeightMetric);
        //     var max = leaves.Max(l => l.WeightMetric);
        //     var range = max - min;
        //     foreach (var leaf in leaves)
        //     {
        //         leaf.NormalizedWeightMetric = range <= double.Epsilon
        //             ? 0.5
        //             : Math.Sqrt((leaf.WeightMetric - min) / range);
        //     }
        //
        // Math.Sqrt keeps real distances but compresses the outliers; for very skewed data the
        // logarithm dampens harder:
        //     Math.Log(leaf.WeightMetric - min + 1) / Math.Log(range + 1)

        leaves.Sort((a, b) => a.WeightMetric.CompareTo(b.WeightMetric));

        var count = leaves.Count;
        var index = 0;
        while (index < count)
        {
            // Find the run of leaves sharing the same weight and give all of them the
            // percentile of their average rank.
            var last = index;
            while (last + 1 < count && leaves[last + 1].WeightMetric.Equals(leaves[index].WeightMetric))
            {
                last++;
            }

            var averageRank = (index + last) / 2.0;
            var percentile = averageRank / (count - 1);
            for (var i = index; i <= last; i++)
            {
                leaves[i].NormalizedWeightMetric = percentile;
            }

            index = last + 1;
        }
    }

    /// <summary>Collapses single-child chains (e.g. a folder that only contains one sub-folder).</summary>
    public HotspotNode Shrink()
    {
        return _children.Count == 1 ? _children[0].Shrink() : this;
    }

    private void CollectLeaves(List<HotspotNode> leaves)
    {
        if (IsLeafNode)
        {
            leaves.Add(this);
        }

        foreach (var child in _children)
        {
            child.CollectLeaves(leaves);
        }
    }

    private static void RemoveLeafNodesWithoutArea(HotspotNode data)
    {
        foreach (var child in data._children)
        {
            RemoveLeafNodesWithoutArea(child);
        }

        // New empty nodes may arise during the recursive process, so this runs bottom to top.
        data._children.RemoveAll(x => x.IsLeafNode && (double.IsNaN(x.AreaMetric) || Math.Abs(x.AreaMetric) <= 0));
    }
}
