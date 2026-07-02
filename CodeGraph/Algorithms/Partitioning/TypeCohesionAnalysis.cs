using CodeGraph.Graph;

namespace CodeGraph.Algorithms.Partitioning;

/// <summary>
///     One row of the cohesion result: a class that decomposes into several independent member
///     groups (partitions) and is therefore a candidate for splitting.
/// </summary>
public class TypeCohesionInfo(CodeElement type, int partitionCount, int memberCount)
{
    public CodeElement Type { get; } = type;

    /// <summary>
    ///     Number of independent partitions the class splits into. Members are connected when they
    ///     call each other or share a field, so 1 means fully cohesive and N>=2 means the class is
    ///     really N separable units. This is the connected-components view of cohesion (LCOM4).
    /// </summary>
    public int PartitionCount { get; } = partitionCount;

    /// <summary>Number of direct members - size/priority context for the split.</summary>
    public int MemberCount { get; } = memberCount;
}

/// <summary>
///     Looks inside classes and flags those that are secretly several classes: their members fall
///     into independent groups that do not interact. Built on <see cref="CodeElementPartitioner" />;
///     the partition count is the cohesion signal, and each flagged class can be inspected with the
///     existing partition view.
/// </summary>
public static class TypeCohesionAnalysis
{
    /// <summary>
    ///     A class with fewer methods than this has too little behaviour for cohesion to be
    ///     meaningful; it is treated as a data holder (DTO/record-like) and skipped, otherwise every
    ///     data class would show up as maximally "incohesive" (each field its own partition).
    /// </summary>
    private const int MinMethodsToAnalyze = 2;

    public static List<TypeCohesionInfo> Calculate(Graph.CodeGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var result = new List<TypeCohesionInfo>();

        foreach (var type in graph.Nodes.Values)
        {
            if (type.ElementType != CodeElementType.Class || type.IsExternal)
            {
                continue;
            }

            var methodCount = type.Children.Count(c => c.ElementType == CodeElementType.Method);
            if (methodCount < MinMethodsToAnalyze)
            {
                continue;
            }

            // Base classes are not folded in (CodeElementPartitioner limitation): cohesion is
            // measured over the class's own declared members only.
            var partitions = CodeElementPartitioner.GetPartitions(graph, type, false);
            if (partitions.Count < 2)
            {
                continue; // Cohesive - not a split candidate.
            }

            result.Add(new TypeCohesionInfo(type, partitions.Count, type.Children.Count));
        }

        return result
            .OrderByDescending(r => r.PartitionCount)
            .ThenByDescending(r => r.MemberCount)
            .ThenBy(r => r.Type.FullName)
            .ToList();
    }
}
