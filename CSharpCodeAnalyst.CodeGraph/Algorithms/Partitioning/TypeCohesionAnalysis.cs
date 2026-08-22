using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.CodeGraph.Algorithms.Partitioning;

/// <summary>
///     One row of the cohesion result: a class that decomposes into several independent member
///     groups (partitions) and is therefore a candidate for splitting.
/// </summary>
public class TypeCohesionInfo(CodeElement type, int partitionCount, int methodCount, double largestPartitionShare)
{
    public CodeElement Type { get; } = type;

    /// <summary>
    ///     Number of independent partitions the class splits into. Members are connected when they
    ///     call each other or share a field, so 1 means fully cohesive and N>=2 means the class is
    ///     really N separable units. This is the connected-components view of cohesion (LCOM4).
    /// </summary>
    public int PartitionCount { get; } = partitionCount;

    /// <summary>
    ///     Number of methods the split is about - size/priority context. Constructors are not part of
    ///     the analysis and are not counted here either. This is deliberately not the number of
    ///     members: a class can carry a lot of state and still have almost no behavior to separate,
    ///     and it is the behavior that has to be moved when the class is split.
    /// </summary>
    public int MethodCount { get; } = methodCount;

    /// <summary>
    ///     Fraction (0..1) of the methods that sit in the biggest partition. Near 1 means one
    ///     dominant group plus a few stray methods (a trivial split); near 1/PartitionCount means an
    ///     even, genuine split between separate responsibilities.
    /// </summary>
    public double LargestPartitionShare { get; } = largestPartitionShare;
}

/// <summary>
///     The partitions of a class, told apart by whether they carry behavior.
/// </summary>
/// <param name="Behavior">The groups holding at least one method - the ways the class splits.</param>
/// <param name="DetachedState">
///     Members of the groups that hold no method at all. Without the constructor, state that no
///     method of the class touches is connected to nothing: fields the constructor only stores, and
///     properties that exist for the outside world (a binding, a caller) rather than for the class
///     itself. It says nothing about how the behavior decomposes, so it is kept out of the count -
///     but it must still be shown somewhere, or members of the class would appear nowhere at all.
/// </param>
public sealed record CohesionPartitions(List<HashSet<string>> Behavior, HashSet<string> DetachedState);

/// <summary>
///     Looks inside classes and flags those that are secretly several classes: their members fall
///     into independent groups that do not interact. Built on <see cref="CodeElementPartitioner" />;
///     the partition count is the cohesion signal, and each flagged class can be inspected with the
///     existing partition view.
/// </summary>
public static class TypeCohesionAnalysis
{
    /// <summary>
    ///     A class with fewer methods than this has too little behavior for cohesion to be
    ///     meaningful; it is treated as a data holder (DTO/record-like) and skipped, otherwise every
    ///     data class would show up as maximally "incohesive" (each field its own partition).
    ///     <para>
    ///         This is a judgment about the input only. It used to double as a performance brake,
    ///         which it no longer has to: the partitioning is linear in the members of the class
    ///         since <see cref="CodeElementPartitioner" /> stopped cloning the graph per class.
    ///     </para>
    /// </summary>
    private const int DefaultMinBehaviorMembers = 4;

    /// <param name="minBehaviorMembers">
    ///     See <see cref="DefaultMinBehaviorMembers" />. Raising it hides small classes, and a small
    ///     class that splits cleanly is usually the easier refactoring of the two.
    /// </param>
    public static List<TypeCohesionInfo> Calculate(Graph.CodeGraph graph, int minBehaviorMembers = DefaultMinBehaviorMembers)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var result = new List<TypeCohesionInfo>();

        foreach (var type in graph.Nodes.Values)
        {
            if (type.ElementType != CodeElementType.Class || type.IsExternal)
            {
                continue;
            }

            // The gate counts exactly what the analysis will see. Counting constructors here would
            // let a class with nine constructor overloads and one method pass as "has behavior".
            var behaviorIds = GetBehaviorIds(type);
            if (behaviorIds.Count < minBehaviorMembers)
            {
                continue;
            }

            // Base classes are folded in as connectors, lifecycle members are left out entirely:
            // a constructor touches most of the state and would merge everything into one partition.
            var partitions = CodeElementPartitioner.GetPartitions(graph, type, PartitionOptions.Cohesion);
            var behavior = Split(behaviorIds, partitions).Behavior;

            if (behavior.Count < 2)
            {
                continue; // Cohesive - not a split candidate.
            }

            var sizes = behavior.Select(p => p.Count(behaviorIds.Contains)).ToList();
            var largestShare = (double)sizes.Max() / sizes.Sum();

            result.Add(new TypeCohesionInfo(type, behavior.Count, behaviorIds.Count, largestShare));
        }

        return result
            .OrderByDescending(r => r.PartitionCount)
            .ThenByDescending(r => r.MethodCount)
            .ThenBy(r => r.Type.FullName)
            .ToList();
    }

    /// <summary>
    ///     Tells the groups that carry behavior apart from the state nothing in the class touches.
    ///     Used by the analysis to count and by the partition view to present, so that both say the
    ///     same thing about the same class.
    /// </summary>
    public static CohesionPartitions Split(CodeElement type, IEnumerable<HashSet<string>> partitions)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(partitions);

        return Split(GetBehaviorIds(type), partitions);
    }

    private static HashSet<string> GetBehaviorIds(CodeElement type)
    {
        return type.Children.Where(IsBehaviorMember).Select(c => c.Id).ToHashSet();
    }

    private static CohesionPartitions Split(HashSet<string> behaviorIds, IEnumerable<HashSet<string>> partitions)
    {
        var behavior = new List<HashSet<string>>();
        var detached = new HashSet<string>();
        foreach (var partition in partitions)
        {
            if (partition.Any(behaviorIds.Contains))
            {
                behavior.Add(partition);
            }
            else
            {
                detached.UnionWith(partition);
            }
        }

        return new CohesionPartitions(behavior, detached);
    }

    /// <summary>
    ///     What the metric counts as behavior: a method that is not there purely to initialize or
    ///     tear down the object. Properties, fields and events ride along inside the partitions -
    ///     they are the state the behavior is grouped by - but a group made of nothing but state is
    ///     not a way the class splits.
    /// </summary>
    private static bool IsBehaviorMember(CodeElement element)
    {
        return element.ElementType == CodeElementType.Method && !CodeElementPartitioner.IsLifecycleMember(element);
    }
}