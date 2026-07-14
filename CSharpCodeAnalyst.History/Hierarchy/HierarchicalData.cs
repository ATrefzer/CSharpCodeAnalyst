using System.Collections;
using CSharpCodeAnalyst.Contracts;

namespace CSharpCodeAnalyst.History.Hierarchy;

/// <summary>
///     The one implementation of <see cref="IHierarchicalData" /> shared by the analyzers (which
///     build it) and the tree-map control (which renders it via the interface).
/// 
///     Coloring:
///     For a leaf node:
///     - If a color key is set (not null) it is used for rendering
///     - If not the weight metric is used to derive a color
///     For a non leaf node the weight is 0, so the hierarchy renders with the default color.
///     Metrics:
///     If an area is not set explicitly it is NaN (used for folders). If we remove leaf nodes and
///     an inner node becomes a leaf, its area is still NaN - RemoveLeafNodesWithoutArea removes it.
///     Weights are always provided raw (e.g. commit counts). The tree-map view normalizes them via
///     NormalizeWeightMetrics, so data producers do not need to know about the color mapping.
/// </summary>
[Serializable]
public sealed class HierarchicalData : IHierarchicalData
{
    private const string PathSeparator = "/";

    private readonly List<HierarchicalData> _children = new();

    public HierarchicalData(string name)
    {
        Name = name;
        Description = Name;
        AreaMetric = double.NaN;
        AreaMetricSum = 0.0;
        WeightMetric = 0.0;
        NormalizedWeightMetric = 0.0;
    }

    /// <summary>
    ///     Leaf node must provide an area metric.
    /// </summary>
    public HierarchicalData(string name, double areaMetric)
    {
        Name = name;
        Description = Name;
        AreaMetric = areaMetric;
        AreaMetricSum = 0.0;
        WeightMetric = 0.0;
        NormalizedWeightMetric = 0.0;
    }

    public HierarchicalData(string name, double areaMetric, double weightMetric)
    {
        Name = name;
        Description = Name;
        AreaMetric = areaMetric;
        AreaMetricSum = 0.0;
        WeightMetric = weightMetric;
        NormalizedWeightMetric = 0.0;
    }

    public double AreaMetric { get; }

    public double AreaMetricSum { get; private set; }

    public IReadOnlyCollection<IHierarchicalData> Children
    {
        get => _children.AsReadOnly();
    }

    public string? ColorKey { get; set; }

    public string Description { get; set; }

    public bool IsLeafNode
    {
        get => Children.Count == 0;
    }

    public string Name { get; }

    public double NormalizedWeightMetric { get; private set; }

    public IHierarchicalData? Parent { get; set; }

    /// <summary>
    ///     Needs to be serializable
    /// </summary>
    public object? Tag { get; set; }

    public double WeightMetric { get; }

    /// <summary>
    ///     No layout information!
    /// </summary>
    public IHierarchicalData Clone()
    {
        var root = Clone(this);
        return root;
    }

    /// <summary>
    ///     Returns the number of all tree nodes in the sub tree.
    /// </summary>
    public int CountLeafNodes()
    {
        if (IsLeafNode)
        {
            return 1;
        }

        var count = 0;
        foreach (var child in Children)
        {
            count += child.CountLeafNodes();
        }

        return count;
    }

    public string GetPathToRoot()
    {
        var path = new List<string>();
        IHierarchicalData? current = this;
        while (current != null)
        {
            path.Add(current.Name);
            current = current.Parent;
        }

        path.Reverse();

        // Note that an artificial root node with name "" takes automatically care
        // that the path starts with a /. But we want to do so in all cases.
        var description = string.Join(PathSeparator, path);
        if (!description.StartsWith(PathSeparator, StringComparison.InvariantCulture))
        {
            description = PathSeparator + description;
        }

        return description;
    }


    /// <summary>
    ///     The weight metric is normalized only across the leaf nodes, using a rank-based
    ///     (percentile) mapping: weights like commit counts are heavily skewed, so with min-max
    ///     a single outlier gets all the color and almost every other leaf is compressed into
    ///     the bottom of the color scale. With percentiles the median leaf sits in the middle
    ///     of the color ramp. Ties share the same percentile (average rank), so equal weights
    ///     always get equal colors - including the all-equal case (0.5), which min-max cannot
    ///     handle at all (division by zero).
    /// </summary>
    public void NormalizeWeightMetrics()
    {
        var leaves = new List<HierarchicalData>();
        CollectLeaves(leaves);

        if (leaves.Count == 0)
        {
            return;
        }

        if (leaves.Count == 1)
        {
            // Degenerate case of "all weights equal": middle of the scale.
            leaves[0].NormalizedWeightMetric = 0.5;
            return;
        }

        leaves.Sort((a, b) => a.WeightMetric.CompareTo(b.WeightMetric));


        // Damped min max mapping with sqrt (keep proportions)
        // var min = leaves.Min(l => l.WeightMetric);
        // var max = leaves.Max(l => l.WeightMetric);
        // var range = max - min;
        // foreach (var leaf in leaves)
        // {
        //     leaf.NormalizedWeightMetric = range <= double.Epsilon
        //         ? 0.5
        //         : Math.Sqrt((leaf.WeightMetric - min) / range);
        // }

        // Damped min max mapping with log (keep proportions)
        // Math.Sqrt keeps real distances but compresses the outliers; for very skewed data
        // the logarithm dampens harder:
        // Math.Log(leaf.WeightMetric - min + 1) / Math.Log(range + 1)


       
        // Rank based (percentile method)
        // The rank-based mapping below encodes only the ORDER of the leaves -
        // distances are lost (the 2nd hottest leaf looks almost as red as the hottest, even
        // if it has a fraction of the weight). If the proportions matter more than the
        // ordering, replace everything below with a dampened min-max mapping instead:
        
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

    /// <summary>
    ///     Note that during the process new leaf nodes may arise.
    ///     Call RemoveLeafNodesWithoutArea to remove them.
    /// </summary>
    public void RemoveLeafNodes(Func<IHierarchicalData, bool> removePredicate)
    {
        RemoveLeafNodes(this, removePredicate);
    }

    /// <summary>
    ///     Removes leaf node where area is not set.
    ///     If new leaf nodes arise during the process they are also removed!
    /// </summary>
    public void RemoveLeafNodesWithoutArea()
    {
        RemoveLeafNodesWithoutArea(this);

        if (IsLeafNode && double.IsNaN(AreaMetric))
        {
            throw new Exception("Hierarchical data is not valid. Singular root node does not have an area.");
        }
    }

    public IHierarchicalData Shrink()
    {
        if (_children.Count == 1)
        {
            return _children.First().Shrink();
        }

        // Leaf node or more than one child.
        return this;
    }

    /// <summary>
    ///     Updates the area metrics from the children up to the root node.
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

        // Non leaf node
        var sum = 0.0;
        foreach (var child in _children)
        {
            child.SumAreaMetrics();
            sum += child.AreaMetricSum;
        }

        AreaMetricSum = sum;

        // Treemap algorithm works best if processed in decreasing order
        _children.Sort(new DecreasingByAreaMetricSumComparer());
    }

    public void TraverseBottomUp(Action<IHierarchicalData> action)
    {
        foreach (var child in Children)
        {
            child.TraverseBottomUp(action);
        }

        // First children, then the parent nodes
        action(this);
    }

    public void TraverseTopDown(Action<IHierarchicalData> action)
    {
        action(this);

        foreach (var child in Children)
        {
            child.TraverseTopDown(action);
        }
    }

    public IEnumerator<IHierarchicalData> GetEnumerator()
    {
        var queue = new Queue<IHierarchicalData>();
        queue.Enqueue(this);

        while (queue.Any())
        {
            var node = queue.Dequeue();
            foreach (var child in node.Children)
            {
                queue.Enqueue(child);
            }

            yield return node;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }


    public static HierarchicalData NoData()
    {
        return new HierarchicalData("NO DATA", 1);
    }

    public void AddChild(HierarchicalData child)
    {
        _children.Add(child);
        child.Parent = this;
    }

    private HierarchicalData Clone(HierarchicalData cloneThis)
    {
        var newData = new HierarchicalData(cloneThis.Name, cloneThis.AreaMetric, cloneThis.WeightMetric)
        {
            Description = cloneThis.Description,
            ColorKey = cloneThis.ColorKey,
            Tag = cloneThis.Tag,
            AreaMetricSum = cloneThis.AreaMetricSum,
            NormalizedWeightMetric = cloneThis.NormalizedWeightMetric
        };

        foreach (var child in cloneThis._children)
        {
            newData.AddChild(Clone(child));
        }

        return newData;
    }

    private void CollectLeaves(List<HierarchicalData> leaves)
    {
        if (IsLeafNode)
        {
            leaves.Add(this);
        }

        foreach (var hierarchicalData in Children)
        {
            var child = (HierarchicalData)hierarchicalData;
            child.CollectLeaves(leaves);
        }
    }

    private void RemoveLeafNodes(HierarchicalData root, Func<IHierarchicalData, bool> removePredicate)
    {
        foreach (var hierarchicalData in root.Children)
        {
            var child = (HierarchicalData)hierarchicalData;
            RemoveLeafNodes(child, removePredicate);
        }

        root._children.RemoveAll(x => x.IsLeafNode && removePredicate(x));
    }

    private void RemoveLeafNodesWithoutArea(HierarchicalData data)
    {
        foreach (var child in data._children)
        {
            RemoveLeafNodesWithoutArea(child);
        }

        // During the recursive process new empty nodes may arise. So bottom to top.
        data._children.RemoveAll(x => x.IsLeafNode && (double.IsNaN(x.AreaMetric) || Math.Abs(x.AreaMetric) <= 0));
    }
}