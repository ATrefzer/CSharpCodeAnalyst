namespace CSharpCodeAnalyst.CodeGraph.Algorithms.Partitioning;

/// <summary>
///     Disjoint set over element ids. Every id starts in its own partition; merging is near
///     constant time, so partitioning costs O(members + relationships) per container.
/// </summary>
public sealed class UnionFind
{
    private readonly Dictionary<string, string> _parent;
    private readonly Dictionary<string, int> _rank;

    public UnionFind(IEnumerable<string> ids)
    {
        _parent = ids.ToDictionary(id => id, id => id);
        _rank = new Dictionary<string, int>(_parent.Count);
    }

    public void Union(string left, string right)
    {
        var leftRoot = Find(left);
        var rightRoot = Find(right);
        if (leftRoot == rightRoot)
        {
            return;
        }

        var leftRank = _rank.GetValueOrDefault(leftRoot);
        var rightRank = _rank.GetValueOrDefault(rightRoot);

        if (leftRank < rightRank)
        {
            _parent[leftRoot] = rightRoot;
        }
        else if (leftRank > rightRank)
        {
            _parent[rightRoot] = leftRoot;
        }
        else
        {
            // If you move left below right and both have the same depths the result tree gets deeper!
            _parent[rightRoot] = leftRoot;
            _rank[leftRoot] = leftRank + 1;
        }
    }

    public List<HashSet<string>> GetGroups()
    {
        var groups = new Dictionary<string, HashSet<string>>();
        foreach (var id in _parent.Keys)
        {
            var root = Find(id);
            if (!groups.TryGetValue(root, out var group))
            {
                group = [];
                groups[root] = group;
            }

            group.Add(id);
        }

        return groups.Values.ToList();
    }

    private string Find(string id)
    {
        var root = id;
        while (_parent[root] != root)
        {
            root = _parent[root];
        }

        // Path compression.
        var current = id;
        while (_parent[current] != root)
        {
            var next = _parent[current];
            _parent[current] = root;
            current = next;
        }

        return root;
    }
}