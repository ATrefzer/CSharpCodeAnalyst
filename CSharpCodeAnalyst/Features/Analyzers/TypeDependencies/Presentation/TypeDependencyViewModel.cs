using System.Globalization;
using CodeGraph.Algorithms.Metrics;
using CodeGraph.Graph;
using CSharpCodeAnalyst.Shared.DynamicDataGrid.Contracts.TabularData;

namespace CSharpCodeAnalyst.Features.Analyzers.TypeDependencies.Presentation;

public class TypeDependencyViewModel : TableRow
{
    internal TypeDependencyViewModel(TypeHotspot hotspot)
    {
        Element = hotspot.Type;
        Rank = hotspot.Rank;
        Name = hotspot.Type.FullName;
        FanIn = hotspot.FanIn;
        FanOut = hotspot.FanOut;

        // Bound for display; sorting uses the numeric Score below via SortMemberName.
        ScoreValue = hotspot.Score;
        Score = hotspot.Score.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>The underlying graph node, used to add the type to the Code Explorer.</summary>
    public CodeElement Element { get; }

    public int Rank { get; }
    public string Name { get; }
    public int FanIn { get; }
    public int FanOut { get; }
    public string Score { get; }
    public double ScoreValue { get; }
}
