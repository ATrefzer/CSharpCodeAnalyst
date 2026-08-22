using System.Globalization;
using CSharpCodeAnalyst.AnalyzerSdk.DynamicDataGrid.Contracts.TabularData;
using CSharpCodeAnalyst.CodeGraph.Algorithms.Partitioning;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.Analyzers.TypeCohesion.Presentation;

public class TypeCohesionRowViewModel : TableRow
{
    internal TypeCohesionRowViewModel(TypeCohesionInfo info)
    {
        Element = info.Type;
        Name = info.Type.FullName;
        Partitions = info.PartitionCount;
        Methods = info.MethodCount;

        // Bound for display; sorting uses the numeric value below via SortMemberName.
        LargestShareValue = info.LargestPartitionShare;
        LargestShare = info.LargestPartitionShare.ToString("P0", CultureInfo.InvariantCulture);
    }

    /// <summary>The underlying class node, used to drill into its partitions.</summary>
    public CodeElement Element { get; }

    public string Name { get; }
    public int Partitions { get; }
    /// <summary>Methods the split is about. Constructors are not part of the analysis.</summary>
    public int Methods { get; }
    public string LargestShare { get; }
    public double LargestShareValue { get; }
}
