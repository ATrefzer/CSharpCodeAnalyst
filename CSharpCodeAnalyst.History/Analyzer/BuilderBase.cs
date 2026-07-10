using System.Diagnostics;
using CSharpCodeAnalyst.Contracts;
using CSharpCodeAnalyst.History.Extensions;
using CSharpCodeAnalyst.History.Hierarchy;
using CSharpCodeAnalyst.History.Model;

namespace CSharpCodeAnalyst.History.Analyzer;

public abstract class BuilderBase
{
    protected abstract HierarchicalData CreateLeafNode(string leafName, Artifact item);

    protected IHierarchicalData Build(List<Artifact> artifacts)
    {
        var data = BuildHierarchy(artifacts);

        try
        {
            // Filtering in InsertLeaf can leave branch nodes with no accepted children behind -
            // structurally leaves, but with no area (NaN). Remove them; throws itself if nothing
            // at all is left. Everything else (area sums, sorting, weight normalization) is
            // visualization work and owned by the tree-map side - this analyzer only collects
            // the raw data.
            data.RemoveLeafNodesWithoutArea();
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex.Message);
            return HierarchicalData.NoData();
        }

        return data.Shrink();
    }

    private HierarchicalData GetBranch(HierarchicalData parent, string branch)
    {
        var found = parent.Children.FirstOrDefault(child => child.Name == branch);
        if (found is not null)
        {
            return (HierarchicalData) found;
        }

        var newBranch = new HierarchicalData(branch);
        parent.AddChild(newBranch);

        // Only once the parent relation is set - GetPathToRoot needs it.
        newBranch.Description = newBranch.GetPathToRoot();
        return newBranch;
    }


    /// <summary>
    ///     Each part of the file path becomes a branch node containing the remainder of the path. The
    ///     file name itself is a leaf node holding the weight and size.
    /// </summary>
    private HierarchicalData BuildHierarchy(List<Artifact> items)
    {
        // Removed later if not needed. The empty root node makes sure the / appears in front of
        // every path.
        var artificialRoot = new HierarchicalData("");

        foreach (var artifact in items)
        {
            var parts = artifact.ServerPath.Split(['/'], StringSplitOptions.RemoveEmptyEntries);
            Insert(artificialRoot, artifact, parts);
        }

        if (artificialRoot.Children.Count == 1)
        {
            // Skip the artificial root node if the data provides its own single root.
            var root = (HierarchicalData) artificialRoot.Children.First();
            root.Parent = null;
            return root;
        }

        return artificialRoot;
    }

    private void Insert(HierarchicalData parent, Artifact item, string[] parts)
    {
        if (parts.Length == 1)
        {
            InsertLeaf(parent, item, parts[0]);
            return;
        }

        var branch = GetBranch(parent, parts[0]);
        Insert(branch, item, parts.Subset(1));
    }

    private void InsertLeaf(HierarchicalData parent, Artifact item, string leafName)
    {
        if (!IsAccepted(item))
        {
            // Area = 0 (no code lines) or weight = 0 (no commits) would break the normalization math.
            return;
        }

        var leaf = CreateLeafNode(leafName, item);
        parent.AddChild(leaf);
    }

    protected abstract bool IsAccepted(Artifact item);
}
