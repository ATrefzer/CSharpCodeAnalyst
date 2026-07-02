using CodeGraph.Graph;

namespace CodeGraph.Algorithms.Partitioning;

/// <summary>
///     Finds disjunct partitions of code elements that are related to each other.
///     This helps to split large classes with low cohesion.
/// </summary>
public class CodeElementPartitioner
{
    public static List<HashSet<string>> GetPartitions(Graph.CodeGraph codeGraph, CodeElement parentElement, bool includeBaseClasses)
    {
        // When base classes are included, their members join the graph only as "connectors": they
        // link the class's own members that interact through shared inherited state / behaviour, but
        // are removed from the reported partitions afterward (a split concerns the own members).
        var baseMemberIds = new HashSet<string>();
        var subGraph = includeBaseClasses
            ? CreateSubGraphWithBaseConnectors(codeGraph, parentElement, baseMemberIds)
            : CreateSubGraph(codeGraph, parentElement);

        var codeElementIdToPartition = InitializePartitions(subGraph);

        var allRelationships = subGraph.GetAllRelationships().ToList();
        foreach (var relationship in allRelationships)
        {
            var sourcePartition = codeElementIdToPartition[relationship.SourceId];
            var targetPartition = codeElementIdToPartition[relationship.TargetId];

            if (ReferenceEquals(sourcePartition, targetPartition))
            {
                // Already in the same partition
                continue;
            }

            MergePartitions(codeElementIdToPartition, sourcePartition, targetPartition);
        }

        var partitions = GetDistinctPartitions(codeElementIdToPartition);

        if (baseMemberIds.Count > 0)
        {
            // Project onto the class's own members: base members were connectors only.
            partitions = partitions
                .Select(partition =>
                {
                    partition.ExceptWith(baseMemberIds);
                    return partition;
                })
                .Where(partition => partition.Count > 0)
                .ToList();
        }

        return partitions;
    }

    private static Graph.CodeGraph CreateSubGraph(Graph.CodeGraph codeGraph, CodeElement parentElement)
    {
        var subGraph = codeGraph.SubGraphOf(parentElement);

        // Remove the single parent element to avoid that everything is in one partition
        subGraph.RemoveCodeElement(parentElement.Id);
        return subGraph;
    }

    /// <summary>
    ///     Builds the sub graph of the class's own members plus the members of its (in-solution) base
    ///     classes as connectors. Own members are linked by any relationship; edges that touch a base
    ///     member connect only via <see cref="RelationshipType.Calls" /> / <see cref="RelationshipType.Uses" />
    ///     (real member interaction / shared state), so structural edges like Overrides do not merge
    ///     members artificially. The visited base member ids are collected into
    ///     <paramref name="baseMemberIds" /> so the caller can project them out again.
    /// </summary>
    private static Graph.CodeGraph CreateSubGraphWithBaseConnectors(Graph.CodeGraph codeGraph, CodeElement parentElement,
        HashSet<string> baseMemberIds)
    {
        var ownIds = parentElement.GetChildrenIncludingSelf();

        var baseClasses = GetBaseClasses(codeGraph, parentElement);
        var baseContainerIds = baseClasses.Select(b => b.Id).ToHashSet();
        foreach (var baseClass in baseClasses)
        {
            baseMemberIds.UnionWith(baseClass.GetChildrenIncludingSelf());
        }

        var includedIds = new HashSet<string>(ownIds);
        includedIds.UnionWith(baseMemberIds);

        var subGraph = codeGraph.Clone(IncludeRelationship, includedIds);

        // Remove the container nodes so they don't force all their members into one partition.
        subGraph.RemoveCodeElement(parentElement.Id);
        foreach (var baseContainerId in baseContainerIds)
        {
            subGraph.RemoveCodeElement(baseContainerId);
        }

        return subGraph;

        bool IncludeRelationship(Relationship relationship)
        {
            if (!includedIds.Contains(relationship.SourceId) || !includedIds.Contains(relationship.TargetId))
            {
                return false;
            }

            // Own <-> own: keep the existing behaviour (any relationship connects).
            if (ownIds.Contains(relationship.SourceId) && ownIds.Contains(relationship.TargetId))
            {
                return true;
            }

            // Anything touching a base member connects only through real interaction.
            return relationship.Type is RelationshipType.Calls or RelationshipType.Uses;
        }
    }

    /// <summary>
    ///     Returns the in-solution base classes of the given class, walking the Inherits chain.
    ///     External base classes are skipped (their members are not in the graph).
    /// </summary>
    private static List<CodeElement> GetBaseClasses(Graph.CodeGraph codeGraph, CodeElement element)
    {
        var baseClasses = new List<CodeElement>();
        var visited = new HashSet<string>();
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

    private static void MergePartitions(Dictionary<string, HashSet<string>> codeElementIdToPartition, HashSet<string> sourcePartition, HashSet<string> targetPartition)
    {
        sourcePartition.UnionWith(targetPartition);
        foreach (var codeElementId in targetPartition)
        {
            codeElementIdToPartition[codeElementId] = sourcePartition;
        }
    }

    private static List<HashSet<string>> GetDistinctPartitions(Dictionary<string, HashSet<string>> codeElementIdToPartition)
    {
        return codeElementIdToPartition.Values.Distinct().ToList();
    }

    /// <summary>
    ///     Returns mapping from code element id to its partition (set of code element ids).
    ///     Basically we start with each code element in its own partition.
    ///     However, if we have a parent-child relationship, we consider all children of a code element
    ///     to be in the same partition.
    /// </summary>
    private static Dictionary<string, HashSet<string>> InitializePartitions(Graph.CodeGraph subGraph)
    {
        // We start with each code element in its own partition
        var codeElementIdToPartition = new Dictionary<string, HashSet<string>>((int)subGraph.VertexCount);
        foreach (var codeElement in subGraph.Nodes.Values)
        {
            codeElementIdToPartition[codeElement.Id] = [];
        }

        // For the moment consider any sub-container as a single element.
        // So we already can initialize the partitions with all children of a code element
        foreach (var codeElement in subGraph.Nodes.Values)
        {
            var partition = codeElementIdToPartition[codeElement.Id];
            var children = codeElement.GetChildrenIncludingSelf();
            foreach (var child in children)
            {
                // Merge partitions.
                partition.Add(child);
                codeElementIdToPartition[child] = partition;
            }
        }

        return codeElementIdToPartition;
    }
}
