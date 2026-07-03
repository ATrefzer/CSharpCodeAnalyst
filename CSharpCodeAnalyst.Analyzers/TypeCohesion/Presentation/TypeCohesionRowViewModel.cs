using System.Globalization;
using CSharpCodeAnalyst.CodeGraph.Algorithms.Partitioning;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Shared.DynamicDataGrid.Contracts.TabularData;

namespace CSharpCodeAnalyst.Analyzers.TypeCohesion.Presentation;

public class TypeCohesionRowViewModel : TableRow
{
    internal TypeCohesionRowViewModel(TypeCohesionInfo info)
    {
        Element = info.Type;
        Name = info.Type.FullName;
        Partitions = info.PartitionCount;
        Members = info.MemberCount;

        // Bound for display; sorting uses the numeric value below via SortMemberName.
        LargestShareValue = info.LargestPartitionShare;
        LargestShare = info.LargestPartitionShare.ToString("P0", CultureInfo.InvariantCulture);
    }

    /// <summary>The underlying class node, used to drill into its partitions.</summary>
    public CodeElement Element { get; }

    public string Name { get; }
    public int Partitions { get; }
    public int Members { get; }
    public string LargestShare { get; }
    public double LargestShareValue { get; }
}
