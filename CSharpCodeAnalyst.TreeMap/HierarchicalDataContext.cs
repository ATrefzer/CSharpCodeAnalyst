using CSharpCodeAnalyst.TreeMap.Interfaces;

namespace CSharpCodeAnalyst.TreeMap
{
    /// <summary>
    /// Data context that is bound to the TreeMapView or CirclePackingView
    /// </summary>
    public sealed class HierarchicalDataContext
    {
        public HierarchicalDataContext(IHierarchicalData data, IBrushFactory brushFactory)
        {
            Data = data;
            BrushFactory = brushFactory;
        }

        public HierarchicalDataContext Clone()
        {
            // Layout info is lost!
            var clone = new HierarchicalDataContext(Data.Clone(), BrushFactory);
            clone.WeightSemantic = WeightSemantic;
            clone.AreaSemantic = AreaSemantic;
            return clone;
        }

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