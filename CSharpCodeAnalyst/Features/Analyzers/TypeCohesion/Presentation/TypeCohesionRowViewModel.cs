using CodeGraph.Algorithms.Partitioning;
using CodeGraph.Graph;
using CSharpCodeAnalyst.Shared.DynamicDataGrid.Contracts.TabularData;

namespace CSharpCodeAnalyst.Features.Analyzers.TypeCohesion.Presentation;

public class TypeCohesionRowViewModel : TableRow
{
    internal TypeCohesionRowViewModel(TypeCohesionInfo info)
    {
        Element = info.Type;
        Name = info.Type.FullName;
        Partitions = info.PartitionCount;
        Members = info.MemberCount;
    }

    /// <summary>The underlying class node, used to drill into its partitions.</summary>
    public CodeElement Element { get; }

    public string Name { get; }
    public int Partitions { get; }
    public int Members { get; }
}
