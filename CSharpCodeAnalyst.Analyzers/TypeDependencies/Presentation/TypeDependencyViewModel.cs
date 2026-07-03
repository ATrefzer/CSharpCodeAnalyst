using System.Globalization;
using CSharpCodeAnalyst.CodeGraph.Algorithms.Metrics;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Shared.DynamicDataGrid.Contracts.TabularData;

namespace CSharpCodeAnalyst.Analyzers.TypeDependencies.Presentation;

public class TypeDependencyViewModel : TableRow
{
    internal TypeDependencyViewModel(TypeDependencyInfo info)
    {
        Element = info.Type;
        Rank = info.Rank;
        Name = info.Type.FullName;
        FanIn = info.FanIn;
        FanOut = info.FanOut;
        BlastRadius = info.BlastRadius;

        // Bound for display; sorting uses the numeric Score below via SortMemberName.
        ScoreValue = info.Score;
        Score = info.Score.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>The underlying graph node, used to add the type to the Code Explorer.</summary>
    public CodeElement Element { get; }

    public int Rank { get; }
    public string Name { get; }
    public int FanIn { get; }
    public int FanOut { get; }
    public int BlastRadius { get; }
    public string Score { get; }
    public double ScoreValue { get; }
}
