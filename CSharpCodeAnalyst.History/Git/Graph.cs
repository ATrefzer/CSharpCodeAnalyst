using System.Collections;
using System.Diagnostics;
using LibGit2Sharp;

namespace CSharpCodeAnalyst.History.Git;

internal class GitNode
{
    public Scope Scope { get; set; }
    public Commit Commit { get; set; }
}

[DebuggerDisplay("{CommitHash}: P={Parents.Count}, C={Children.Count}")]
public sealed class GraphNode
{
    public GraphNode(string commitHash)
    {
        CommitHash = commitHash;
    }


    public object Commit { get; set; }

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

    private LeaseCommonAncestorPreprocessData _preprocessData;

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

    /// <summary>
    ///     Removes all nodes from the graph that don't end up at the given hash
    /// </summary>
    public void MinimizeTo(string hash)
    {
        // Find relevant nodes. These are nodes that lead to the given hash.
        var relevant = new HashSet<string>();
        var queue = new Queue<GraphNode>();
        queue.Enqueue(GetNode(hash));

        while (queue.Any())
        {
            var node = queue.Dequeue();

            if (relevant.Add(node.CommitHash))
            {
                node.Parents.ForEach(parent => queue.Enqueue(parent));
            }
        }

        // Remove all non relevant nodes
        var toRemove = new HashSet<string>(_hashToGraphNode.Keys.Except(relevant));
        foreach (var node in toRemove)
        {
            _hashToGraphNode.Remove(node);
        }

        foreach (var node in AllNodes)
        {
            node.Parents.RemoveAll(parent => toRemove.Contains(parent.CommitHash));
            node.Children.RemoveAll(child => toRemove.Contains(child.CommitHash));
        }

        GetNode(hash).Children.Clear();
    }

    private LeaseCommonAncestorPreprocessData PreprocessLeastCommonAncestor()
    {
        void TraverseDepthFirst(LeaseCommonAncestorPreprocessData preprocessData, GraphNode node, int currentDepth = 0)
        {
            if (preprocessData.AlreadyProcessed.Add(node) is false)
            {
                return;
            }

            preprocessData.Record(node, currentDepth);
            foreach (var child in node.Children)
            {
                TraverseDepthFirst(preprocessData, child, currentDepth + 1);
                preprocessData.Record(node, currentDepth);
            }
        }

        var data = new LeaseCommonAncestorPreprocessData();
        var root = AllNodes.Single(node => !node.Parents.Any());
        TraverseDepthFirst(data, root);

        return data;
    }

    public string FindCommonAncestor(string hash1, string hash2)
    {
        var node1 = GetNode(hash1);
        var node2 = GetNode(hash2);

        return FindCommonAncestor(node1, node2)?.CommitHash;
    }

    /// <summary>
    ///     See https://www.youtube.com/watch?v=sD1IoalFomA
    ///     The graph has to be rooted. Since the algorithm is intended to work for trees I made an adjustment
    ///     such that I process branching nodes only once.
    ///     Otherwise we would process the same sub graphs again and again.
    /// </summary>
    public GraphNode FindCommonAncestor(GraphNode node1, GraphNode node2)
    {
        if (_preprocessData is null)
        {
            _preprocessData = PreprocessLeastCommonAncestor();
        }

        var index1 = _preprocessData.GraphNodeToIndex[node1];
        var index2 = _preprocessData.GraphNodeToIndex[node2];

        var from = Math.Min(index1, index2);
        var to = Math.Max(index1, index2);

        var lcaIndex = -1;

        var minDepth = int.MaxValue;
        for (var i = from; i <= to; i++)
        {
            if (_preprocessData.Depth[i] < minDepth)
            {
                minDepth = _preprocessData.Depth[i];
                lcaIndex = i;
            }
        }

        return _preprocessData.EulerPath[lcaIndex];
    }

    public void UpdateGraph(string hash, IEnumerable<string> allParents)
    {
        lock (_lockObj)
        {
            _preprocessData = null;

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


    public List<string> GetParentHashes(string hash)
    {
        return GetNode(hash).Parents.ConvertAll(node => node.CommitHash);
    }

    public bool Exists(string hash)
    {
        return _hashToGraphNode.ContainsKey(hash);
    }

    public GraphNode? GetNode(string? hash)
    {
        if (hash == null)
        {
            return null;
        }

        return _hashToGraphNode[hash];
    }

    private class LeaseCommonAncestorPreprocessData
    {

        public readonly HashSet<GraphNode> AlreadyProcessed = new();
        public readonly List<int> Depth = new();
        public readonly List<GraphNode> EulerPath = new();

        /// <summary>
        ///     Last occurrence of a node in the euler path
        /// </summary>
        public readonly Dictionary<GraphNode, int> GraphNodeToIndex = new();

        public void Record(GraphNode node, int depth)
        {
            var nextIndex = EulerPath.Count;
            GraphNodeToIndex[node] = nextIndex;
            EulerPath.Add(node);
            Depth.Add(depth);
        }
    }
}