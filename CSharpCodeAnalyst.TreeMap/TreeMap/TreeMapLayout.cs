using CSharpCodeAnalyst.TreeMap.Interfaces;

namespace CSharpCodeAnalyst.TreeMap.TreeMap
{
    /// <summary>
    ///     Per-node rectangle assignments produced by the tree-map layout. Kept as a side map
    ///     (keyed by node reference) so the pure <see cref="IHierarchicalData" /> tree does not have
    ///     to carry mutable rendering state. Produced by <see cref="SquarifiedTreeMapLayout" /> and
    ///     consumed by the renderer and hit testing.
    /// </summary>
    public sealed class TreeMapLayout
    {
        private readonly Dictionary<IHierarchicalData, RectangularLayoutInfo> _rects = new();

        /// <summary>Returns the node's layout, creating an empty one on first access.</summary>
        public RectangularLayoutInfo GetOrCreate(IHierarchicalData node)
        {
            if (!_rects.TryGetValue(node, out var info))
            {
                info = new RectangularLayoutInfo();
                _rects[node] = info;
            }

            return info;
        }

        /// <summary>Returns the node's layout, or null if it was never laid out.</summary>
        public RectangularLayoutInfo? Get(IHierarchicalData node)
        {
            return _rects.GetValueOrDefault(node);
        }
    }
}
