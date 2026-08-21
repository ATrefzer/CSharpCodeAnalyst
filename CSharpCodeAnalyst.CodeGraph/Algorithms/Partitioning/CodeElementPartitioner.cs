using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.CodeGraph.Algorithms.Partitioning;

/// <summary>
///     Finds disjunct partitions of code elements that are related to each other.
///     This helps to split large classes with low cohesion.
/// </summary>
public static class CodeElementPartitioner
{
    /// <summary>Name the parser gives an instance constructor.</summary>
    private const string ConstructorName = ".ctor";

    /// <summary>Name the parser gives a static constructor.</summary>
    private const string StaticConstructorName = ".cctor";

    /// <summary>Name the parser gives a finalizer.</summary>
    private const string FinalizerName = "Finalize";

    /// <summary>
    ///     Members that exist to bring an object into (or out of) a valid state rather than to do
    ///     work: constructors, the static constructor and the finalizer. A constructor assigns most
    ///     of the state, so in the member graph it is a clique over all fields and merges everything
    ///     into a single partition - see <see cref="PartitionOptions.ExcludeLifecycleMembers" />.
    /// </summary>
    public static bool IsLifecycleMember(CodeElement element)
    {
        return element is
        {
            ElementType: CodeElementType.Method,
            Name: ConstructorName or StaticConstructorName or FinalizerName
        };
    }

    /// <summary>
    ///     Convenience overload for the common case: any relationship connects, lifecycle members
    ///     included.
    /// </summary>
    public static List<HashSet<string>> GetPartitions(Graph.CodeGraph codeGraph, CodeElement parentElement,
        bool includeBaseClasses)
    {
        return GetPartitions(codeGraph, parentElement, new PartitionOptions(includeBaseClasses, false));
    }

    /// <summary>
    ///     Groups the members of <paramref name="parentElement" /> into connected components: two
    ///     members end up in the same partition when they interact directly or through a shared
    ///     element (typically a field). The container itself is never a node, otherwise it would pull
    ///     all its members into a single partition.
    /// </summary>
    public static List<HashSet<string>> GetPartitions(Graph.CodeGraph codeGraph, CodeElement parentElement,
        PartitionOptions options)
    {
        ArgumentNullException.ThrowIfNull(codeGraph);
        ArgumentNullException.ThrowIfNull(parentElement);
        ArgumentNullException.ThrowIfNull(options);

        // The member graph is built directly from the elements. Cloning a sub graph would be easier
        // to read, but Clone walks all nodes of the whole graph, which makes an analysis over every
        // class of a solution quadratic.
        var nodes = new Dictionary<string, CodeElement>();
        var ownIds = CollectMembers(parentElement, options, nodes);

        // Base classes are folded in as connectors: their members link the own members that interact
        // through shared inherited state / behavior, but are removed from the reported partitions
        // afterward (a split concerns the own members).
        var baseMemberIds = new HashSet<string>();
        if (options.IncludeBaseClasses)
        {
            foreach (var baseClass in GetBaseClasses(codeGraph, parentElement))
            {
                baseMemberIds.UnionWith(CollectMembers(baseClass, options, nodes));
            }
        }

        var partitions = new UnionFind(nodes.Keys);

        // For the moment consider any sub-container as a single element: a member is merged with its
        // own subtree (a nested type with its members, a property with its accessors).
        foreach (var (id, element) in nodes)
        {
            foreach (var descendant in element.GetSubtreeIncludingSelf())
            {
                if (nodes.ContainsKey(descendant.Id))
                {
                    partitions.Union(id, descendant.Id);
                }
            }
        }

        foreach (var (id, element) in nodes)
        {
            foreach (var relationship in element.Relationships)
            {
                if (nodes.ContainsKey(relationship.TargetId) && IsConnector(id, relationship))
                {
                    partitions.Union(id, relationship.TargetId);
                }
            }
        }

        var result = partitions.GetGroups();

        if (baseMemberIds.Count > 0)
        {
            // Project onto the own members: base members were connectors only.
            result = result
                .Select(partition =>
                {
                    partition.ExceptWith(baseMemberIds);
                    return partition;
                })
                .Where(partition => partition.Count > 0)
                .ToList();
        }

        // Biggest first, ties broken by name, so the numbering in the partition view is stable.
        return result
            .OrderByDescending(partition => partition.Count)
            .ThenBy(partition => partition.Min(StringComparer.Ordinal), StringComparer.Ordinal)
            .ToList();

        bool IsConnector(string sourceId, Relationship relationship)
        {
            // Own <-> own: any relationship connects.
            if (ownIds.Contains(sourceId) && ownIds.Contains(relationship.TargetId))
            {
                return true;
            }

            // Anything touching a base member connects only through real interaction, so structural
            // edges like Overrides do not merge members artificially.
            return relationship.Type is RelationshipType.Calls or RelationshipType.Uses;
        }
    }

    /// <summary>
    ///     The members of <paramref name="container" /> (the container itself excluded) with their
    ///     subtrees, added to <paramref name="nodes" />. Returns the ids collected here.
    /// </summary>
    private static HashSet<string> CollectMembers(CodeElement container, PartitionOptions options,
        Dictionary<string, CodeElement> nodes)
    {
        var collected = new HashSet<string>();
        foreach (var member in container.Children)
        {
            if (options.ExcludeLifecycleMembers && IsLifecycleMember(member))
            {
                continue;
            }

            foreach (var element in member.GetSubtreeIncludingSelf())
            {
                nodes[element.Id] = element;
                collected.Add(element.Id);
            }
        }

        return collected;
    }

    /// <summary>
    ///     Returns the in-solution base classes of the given class, walking the Inherits chain.
    ///     External base classes are skipped (their members are not in the graph).
    /// </summary>
    private static List<CodeElement> GetBaseClasses(Graph.CodeGraph codeGraph, CodeElement element)
    {
        var baseClasses = new List<CodeElement>();

        // Nothing is a base class of itself. An imported graph may well contain such an edge, and it
        // would wipe out the whole result when the base members are projected out again.
        var visited = new HashSet<string> { element.Id };
        var queue = new Queue<CodeElement>();
        queue.Enqueue(element);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var inheritsFrom = current.Relationships
                .Where(r => r.Type == RelationshipType.Inherits && r.SourceId == current.Id);

            foreach (var relationship in inheritsFrom)
            {
                var baseClass = codeGraph.TryGetCodeElement(relationship.TargetId);
                if (baseClass is null || baseClass.IsExternal)
                {
                    continue;
                }

                if (visited.Add(baseClass.Id))
                {
                    baseClasses.Add(baseClass);
                    queue.Enqueue(baseClass);
                }
            }
        }

        return baseClasses;
    }

    /// <summary>
    ///     Disjoint set over element ids. Every id starts in its own partition; merging is near
    ///     constant time, so partitioning costs O(members + relationships) per container.
    /// </summary>
    private sealed class UnionFind
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
}
