using System.Collections;
using System.Diagnostics;

namespace CSharpCodeAnalyst.History.Git;

[DebuggerDisplay("{CommitHash}: P={Parents.Count}, C={Children.Count}")]
public sealed class GraphNode
{
    public GraphNode(string commitHash)
    {
        CommitHash = commitHash;
    }

    public Scope? Scope { get; set; }

    public string CommitHash { get; }

    public List<GraphNode> Parents { get; } = new();
    public List<GraphNode> Children { get; } = new();
}

/// <summary>
///     Full Git graph containing all commits. Nothing simplified here.
/// </summary>
public class Graph : IEnumerable<GraphNode>
{

    // hash -> node {hash, parent hashes}
    private readonly Dictionary<string, GraphNode> _hashToGraphNode = new();
    private readonly Lock _lockObj = new();


    public IEnumerable<GraphNode> AllNodes
    {
        get => _hashToGraphNode.Values.ToList(); // Copy list
    }

    public IEnumerator<GraphNode> GetEnumerator()
    {
        // Breadth first traversal.

        var processed = new HashSet<GraphNode>();

        var root = AllNodes.Single(node => node.Parents.Count == 0);
        var queue = new Queue<GraphNode>();
        queue.Enqueue(root);
        while (queue.Any())
        {
            var node = queue.Dequeue();

            yield return node;

            foreach (var child in node.Children)
            {
                if (processed.Add(child))
                {
                    queue.Enqueue(child);
                }
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }



    public Graph Clone()
    {
        var newGraph = new Graph();
        foreach (var pair in _hashToGraphNode)
        {
            var node = pair.Value;
            foreach (var parent in node.Parents)
            {
                newGraph.UpdateGraph(pair.Key, parent.CommitHash);
            }
        }

        return newGraph;
    }

    public void UpdateGraph(string hash, IEnumerable<string> allParents)
    {
        lock (_lockObj)
        {
            // GraphNode for the given hash.
            var node = GetOrAddNode(hash);

            // Update parents and child relationships
            foreach (var parentHash in allParents)
            {
                node.Parents.Add(GetOrAddNode(parentHash));

                var parent = GetOrAddNode(parentHash);
                parent.Children.Add(GetOrAddNode(hash));
            }
        }
    }

    /// <summary>
    ///     Adds a new commit to the commit graph.
    ///     This may result in several new nodes in the graph because we add nodes for possible parents in advance.
    /// </summary>
    /// <param name="hash">Commit hash (change set id)</param>
    /// <param name="parents">List of parent commit hashes to extend the graph</param>
    public void UpdateGraph(string hash, string parents)
    {
        var allParents = !string.IsNullOrEmpty(parents)
            ? parents.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(parent => parent)
                .ToList()
            : new List<string>();

        UpdateGraph(hash, allParents);
    }

    public GraphNode GetOrAddNode(string hash)
    {
        if (_hashToGraphNode.TryGetValue(hash, out var node) is false)
        {
            node = new GraphNode(hash);
            _hashToGraphNode.Add(hash, node);
        }

        return node;
    }

    public GraphNode? GetNode(string? hash)
    {
        if (hash == null)
        {
            return null;
        }

        return _hashToGraphNode[hash];
    }
}