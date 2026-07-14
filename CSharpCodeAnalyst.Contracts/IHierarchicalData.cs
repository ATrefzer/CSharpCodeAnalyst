namespace CSharpCodeAnalyst.Contracts
{
    /// <summary>
    ///     Contract for a node in a weighted hierarchy that a tree map renders. This is the only
    ///     type the tree-map control needs from a data producer - the concrete implementation and
    ///     all algorithms live in the producing library, not here.
    /// </summary>
    public interface IHierarchicalData : IEnumerable<IHierarchicalData>
    {
        /// <summary>
        /// null = don't use, empty use default color, otherwise the assigned color.
        /// </summary>
        string? ColorKey { get; set; }
        double AreaMetricSum { get; }
        IReadOnlyCollection<IHierarchicalData> Children { get; }
        bool IsLeafNode { get; }
        double NormalizedWeightMetric { get; }
        string Name { get; }
        double AreaMetric { get; }
        double WeightMetric { get; }
        object? Tag { get; set; }
        IHierarchicalData? Parent { get; set; }
        string Description { get; set; }

        IHierarchicalData Clone();
        int CountLeafNodes();
        string GetPathToRoot();
        void NormalizeWeightMetrics(WeightNormalizationStrategy strategy);
        void RemoveLeafNodes(Func<IHierarchicalData, bool> removePredicate);
        void RemoveLeafNodesWithoutArea();
        IHierarchicalData Shrink();
        void SumAreaMetrics();
        void TraverseBottomUp(Action<IHierarchicalData> action);
        void TraverseTopDown(Action<IHierarchicalData> action);
    }
}
