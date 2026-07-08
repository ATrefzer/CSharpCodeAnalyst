using CSharpCodeAnalyst.TreeMap.Interfaces;

namespace CSharpCodeAnalyst.TreeMap.Tools
{
    /// <summary>
    /// Condition which HierarchicalData item shall be shown highlighted in any graph.
    /// Created from the ToolViewModel that contains all highlighting related settings.
    /// </summary>
    internal sealed class Highlighting : IHighlighting
    {
        private readonly ToolViewModel _toolViewModel;
        private readonly string? _pattern;

        public Highlighting(ToolViewModel tvm)
        {
            _toolViewModel = tvm;
            _pattern = tvm.SearchPattern?.ToLowerInvariant();
        }

        public bool IsHighlighted(IHierarchicalData data)
        {
            var isNameMatchingActive = !string.IsNullOrEmpty(_pattern);
            var isNameMatching = false;
            if (isNameMatchingActive)
            {
                isNameMatching = data.Name.ToLowerInvariant().Contains(_pattern);
            }

            // Matching area and weight criteria is optional.
            var areRangesMatching = _toolViewModel.NoFilterJustHighlight
                && _toolViewModel.IsAreaValid(data.AreaMetric) 
                && _toolViewModel.IsWeightValid(data.WeightMetric);

            if (!isNameMatchingActive && _toolViewModel.NoFilterJustHighlight)
            {
                // If no name is entered ignore the name filtering
                return areRangesMatching;
            }

            if (isNameMatchingActive && !_toolViewModel.NoFilterJustHighlight)
            {
                return isNameMatching;
            }

            if (isNameMatchingActive && _toolViewModel.NoFilterJustHighlight)
            {
                return isNameMatching && areRangesMatching;
            }

            return false;
        }
    }
}