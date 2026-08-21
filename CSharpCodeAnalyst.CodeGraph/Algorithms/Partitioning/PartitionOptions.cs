namespace CSharpCodeAnalyst.CodeGraph.Algorithms.Partitioning;

/// <summary>
///     Which members take part in the partitioning and what may connect them.
/// </summary>
/// <param name="IncludeBaseClasses">
///     Fold the members of the in-solution base classes in as connectors, so that own members which
///     interact only through inherited state or behavior are recognized as connected. The base
///     members themselves never appear in the result.
/// </param>
/// <param name="ExcludeLifecycleMembers">
///     Drop constructors, the static constructor and the finalizer from the member graph. A
///     constructor assigns most of the state, so it connects every field it touches to every other
///     one and hides any split the class actually has. Leaving them out is what makes the partition
///     count a cohesion signal (LCOM4) rather than a reachability check, at the price of state that
///     nothing but the constructor touches ending up in partitions of its own - the caller decides
///     what to do with those.
/// </param>
public sealed record PartitionOptions(bool IncludeBaseClasses, bool ExcludeLifecycleMembers)
{
    /// <summary>
    ///     What the partition view shows: everything the class declares, connected by any
    ///     relationship. Nothing is hidden from the reader.
    /// </summary>
    public static readonly PartitionOptions Complete = new(true, false);

    /// <summary>
    ///     What the cohesion metric measures: behavior only, with lifecycle members removed.
    /// </summary>
    public static readonly PartitionOptions Cohesion = new(true, true);
}
