using CSharpCodeAnalyst.TreeMap.Common;

namespace CSharpCodeAnalyst.TreeMap.Interfaces
{
    public interface IHierarchicalData : IEnumerable<IHierarchicalData>
    {
        // TODO move layoutig code outside.
        /// <summary>
        /// Attached layout information. This property is not cloned
        /// </summary>
        LayoutInfo? Layout { get; set; }
        
        /// <summary>
        /// Unique Id of this node within the hierarchy. (Cloned)
        /// </summary>
        int Id { get; set; }

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
        void NormalizeWeightMetrics();
        void RemoveLeafNodes(Func<IHierarchicalData, bool> removePredicate);
        void RemoveLeafNodesWithoutArea();
        IHierarchicalData Shrink();
        void SumAreaMetrics();
        void TraverseBottomUp(Action<IHierarchicalData> action);
        void TraverseTopDown(Action<IHierarchicalData> action);
    }
}