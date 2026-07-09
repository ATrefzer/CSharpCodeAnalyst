namespace CSharpCodeAnalyst.History.Analyzer;

/// <summary>
///     Self-contained hierarchical tree node for the hotspot analysis. Deliberately independent from
///     CSharpCodeAnalyst.TreeMap.Data.HierarchicalData (which lives in a WPF-only assembly) so this
///     library stays free of any UI dependency.
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

    /// <summary>Leaf node constructor - must provide an area metric. The weight stays raw
    /// (e.g. commit count); normalization for coloring is owned by the tree-map view.</summary>
    public HotspotNode(string name, double areaMetric, double weightMetric)
    {
        Name = name;
        Description = name;
        AreaMetric = areaMetric;
        WeightMetric = weightMetric;
    }
    
    public HotspotNode(string name, double areaMetric, string colorKey)
    {
        Name = name;
        Description = name;
        AreaMetric = areaMetric;
        WeightMetric = double.NaN;
        ColorKey = colorKey;
    }
    
    public string Name { get; }
    public string Description { get; set; }
    public string? ColorKey { get; set; }
    public object? Tag { get; set; }
    public HotspotNode? Parent { get; set; }
    public IReadOnlyList<HotspotNode> Children => _children;
    public bool IsLeafNode => _children.Count == 0;

    public double AreaMetric { get; }
    public double WeightMetric { get; }

    public static HotspotNode NoData()
    {
        return new HotspotNode("No Data", 1, 0);
    }

    public void AddChild(HotspotNode child)
    {
        _children.Add(child);
        child.Parent = this;
    }
    
    public void VisitAll(Action<HotspotNode> action)
    {
        action(this);
        foreach (var child in Children)
        {
            child.VisitAll(action);
        }
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

    /// <summary>Collapses single-child chains (e.g. a folder that only contains one sub-folder).</summary>
    public HotspotNode Shrink()
    {
        return _children.Count == 1 ? _children[0].Shrink() : this;
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
