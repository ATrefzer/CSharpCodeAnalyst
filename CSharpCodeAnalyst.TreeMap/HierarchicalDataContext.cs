using CSharpCodeAnalyst.Contracts;
using CSharpCodeAnalyst.TreeMap.Interfaces;

namespace CSharpCodeAnalyst.TreeMap
{
    /// <summary>
    /// Data context that is bound to the TreeMapView or CirclePackingView
    /// </summary>
    public sealed class HierarchicalDataContext
    {
        public HierarchicalDataContext(IHierarchicalData data, IBrushFactory brushFactory, HierarchicalDataCommands? commands = null)
        {
            Data = data;
            BrushFactory = brushFactory;
            Commands = commands;
        }

        public HierarchicalDataContext Clone()
        {
            // Layout info is lost!
            var clone = new HierarchicalDataContext(Data.Clone(), BrushFactory!)
                {
                    WeightSemantic = WeightSemantic,
                    AreaSemantic = AreaSemantic,
                    CreateNoData = CreateNoData
                };
            return clone;
        }

        /// <summary>
        /// Factory for the placeholder shown when a filter removes every node. The data producer
        /// supplies it because the tree-map control only knows the interface, not a concrete node
        /// type it could instantiate itself.
        /// </summary>
        public Func<IHierarchicalData>? CreateNoData { get; init; }

        public HierarchicalDataContext(IHierarchicalData data)
        {
            Data = data;
            BrushFactory = null;
        }

        /// <summary>
        /// Only needed if we use a color key.
        /// Otherwise, a gradient is used.
        /// </summary>
        public IBrushFactory? BrushFactory { get; }

        public HierarchicalDataCommands? Commands { get; init; }

        public IHierarchicalData Data { get; }

        /// <summary>
        /// User hint what the area means (file size)
        /// </summary>
        public string? AreaSemantic { get; set; }

        /// <summary>
        /// User hint what the weight means (modifications)
        /// </summary>
        public string? WeightSemantic { get; set; }
    }
}