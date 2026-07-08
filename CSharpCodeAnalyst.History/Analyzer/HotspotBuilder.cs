using System.Globalization;
using CSharpCodeAnalyst.History.Config;
using CSharpCodeAnalyst.History.Extensions;
using CSharpCodeAnalyst.History.Metrics;
using CSharpCodeAnalyst.History.Model;

namespace CSharpCodeAnalyst.History.Analyzer;

/// <summary>
///     Builds a hotspot tree (folder hierarchy, area = lines of code, weight = commits) from a flat
///     artifact summary. Standalone on purpose - see the note on <see cref="HotspotNode" />.
/// </summary>
public sealed class HotspotBuilder
{
    private HotspotCalculator _hotspotCalculator = null!;

    public HotspotNode Build(List<Artifact> artifacts, Dictionary<string, LinesOfCodeProvider.LinesOfCode> metrics)
    {
        _hotspotCalculator = new HotspotCalculator(artifacts, metrics);

        var data = BuildHierarchy(artifacts);

        try
        {
            // Must run first: filtering in InsertLeaf can leave branch nodes with no accepted
            // children behind - structurally leaves, but with no area (NaN). SumAreaMetrics would
            // throw on those. Throws itself if nothing at all is left.
            data.RemoveLeafNodesWithoutArea();
            data.SumAreaMetrics();
            data.NormalizeWeightMetrics();
        }
        catch (Exception ex)
        {
            return HotspotNode.NoData();
        }

        return data.Shrink();
    }

    /// <summary>
    ///     Each part of the file path becomes a branch node containing the remainder of the path. The
    ///     file name itself is a leaf node holding the weight and size.
    /// </summary>
    private HotspotNode BuildHierarchy(List<Artifact> items)
    {
        // Removed later if not needed. The empty root node makes sure the / appears in front of
        // every path.
        var artificialRoot = new HotspotNode("");

        foreach (var artifact in items)
        {
            var parts = artifact.ServerPath.Split(['/'], StringSplitOptions.RemoveEmptyEntries);
            Insert(artificialRoot, artifact, parts);
        }

        if (artificialRoot.Children.Count == 1)
        {
            // Skip the artificial root node if the data provides its own single root.
            var root = artificialRoot.Children[0];
            root.Parent = null;
            return root;
        }

        return artificialRoot;
    }

    private double GetArea(Artifact item)
    {
        return _hotspotCalculator.GetLinesOfCode(item);
    }

    private HotspotNode GetBranch(HotspotNode parent, string branch)
    {
        var found = parent.Children.FirstOrDefault(child => child.Name == branch);
        if (found is not null)
        {
            return found;
        }

        var newBranch = new HotspotNode(branch);
        parent.AddChild(newBranch);

        // Only once the parent relation is set - GetPathToRoot needs it.
        newBranch.Description = newBranch.GetPathToRoot();
        return newBranch;
    }

    private string GetDescription(Artifact item)
    {
        var hotspot = _hotspotCalculator.GetHotspotValue(item);
        return item.ServerPath
               + "\nCommits: " + item.Commits
               + "\nLOC: " + _hotspotCalculator.GetLinesOfCode(item)
               + "\nHotspot: " + hotspot.ToString("F5", CultureInfo.InvariantCulture);
    }

    private double GetWeight(Artifact item)
    {
        return _hotspotCalculator.GetCommits(item);
    }

    private void Insert(HotspotNode parent, Artifact item, string[] parts)
    {
        if (parts.Length == 1)
        {
            InsertLeaf(parent, item, parts[0]);
            return;
        }

        var branch = GetBranch(parent, parts[0]);
        Insert(branch, item, parts.Subset(1));
    }

    private void InsertLeaf(HotspotNode parent, Artifact item, string leafName)
    {
        if (!IsAccepted(item))
        {
            // Area = 0 (no code lines) or weight = 0 (no commits) would break the normalization math.
            return;
        }

        var leaf = new HotspotNode(leafName, GetArea(item), GetWeight(item))
        {
            Description = GetDescription(item),
            Tag = item.LocalPath
        };
        parent.AddChild(leaf);
    }

    private bool IsAccepted(Artifact item)
    {
        // Area must be > 0 because of division. A file must have a size (lines of code) and must
        // have been committed often enough to be relevant.
        return GetArea(item) >= Thresholds.MinLinesOfCodeForHotspot && GetWeight(item) >= Thresholds.MinCommitsForHotspots;
    }
}
