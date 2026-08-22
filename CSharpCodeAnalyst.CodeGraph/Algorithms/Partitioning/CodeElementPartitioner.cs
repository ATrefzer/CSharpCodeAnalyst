using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.CodeGraph.Algorithms.Partitioning;

/// <summary>
///     Finds disjunct partitions of code elements that are related to each other.
///     This helps to split large classes with low cohesion.
/// </summary>
public static class CodeElementPartitioner
{
    /// <summary>
    ///     Members that exist to bring an object into (or out of) a valid state rather than to do
    ///     work: constructors, the static constructor and the finalizer. A constructor assigns most
    ///     of the state, so in the member graph it is a clique over all fields and merges everything
    ///     into a single partition - see <see cref="PartitionOptions.ExcludeLifecycleMembers" />.
    ///     <para>
    ///         Which members those are is the producer's statement, not a guess from the name: a C++
    ///         constructor is called like its class and a Dart one may be called anything at all, so
    ///         no name test could hold for more than one language.
    ///     </para>
    /// </summary>
    public static bool IsLifecycleMember(CodeElement element)
    {
        return element.MemberRole.IsLifecycle();
    }

    /// <summary>
    ///     Groups the members of <paramref name="parentElement" /> into connected components: two
    ///     members end up in the same partition when they interact directly or through a shared
    ///     element (typically a field). The container itself is never a node, otherwise it would
    ///     pull all its members into a single partition.
    /// </summary>
    public static List<HashSet<string>> GetPartitions(Graph.CodeGraph codeGraph, CodeElement parentElement, PartitionOptions options)
    {
        ArgumentNullException.ThrowIfNull(codeGraph);
        ArgumentNullException.ThrowIfNull(parentElement);
        ArgumentNullException.ThrowIfNull(options);

        var (nodes, ownIds, baseMemberIds) = CollectNodes(codeGraph, parentElement, options);

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

        // A relationship always sits on the element it starts at, so SourceId identifies the owner.
        foreach (var element in nodes.Values)
        {
            foreach (var relationship in element.Relationships)
            {
                if (Connects(relationship))
                {
                    partitions.Union(relationship.SourceId, relationship.TargetId);
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

        // Whether this relationship joins its two endpoints. Not to be confused with the base
        // members, which are the connector *nodes*: this is about the edge.
        bool Connects(Relationship relationship)
        {
            if (!nodes.ContainsKey(relationship.TargetId))
            {
                // Target is not within the analyzed code elements.
                return false;
            }

            // Own <-> own: any relationship that is a real dependency connects. 
            // "handles" is not included.
            if (ownIds.Contains(relationship.SourceId) && ownIds.Contains(relationship.TargetId))
            {
                return relationship.Type.IsDependency();
            }

            // Anything touching a base member connects only through real interaction, so structural
            // edges like Overrides do not merge members artificially.
            return relationship.Type is RelationshipType.Calls or RelationshipType.Uses;
        }
    }

    /// <summary>
    ///     Builds the node set the partitioning runs on: the own members, plus the base class
    ///     members that act as connectors between them.
    /// </summary>
    private static (Dictionary<string, CodeElement> nodes, HashSet<string> ownIds, HashSet<string> baseMemberIds)
        CollectNodes(Graph.CodeGraph codeGraph, CodeElement parentElement, PartitionOptions options)
    {
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

        return (nodes, ownIds, baseMemberIds);
    }

    /// <summary>
    ///     The members of <paramref name="container" /> (the container itself excluded) with their
    ///     subtrees, added to <paramref name="nodes" />. Returns the ids collected here.
    /// </summary>
    private static HashSet<string> CollectMembers(CodeElement container, PartitionOptions options, Dictionary<string, CodeElement> nodes)
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
    ///     External base classes are skipped (their members may not be in the graph).
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
}