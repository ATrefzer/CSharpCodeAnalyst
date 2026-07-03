using CodeGraph.Graph;
using CodeGraph.Metrics;
using CSharpCodeAnalyst.Shared.DynamicDataGrid.Contracts.TabularData;

namespace CSharpCodeAnalyst.Features.Analyzers.MethodComplexity.Presentation;

public class MethodComplexityRowViewModel : TableRow
{
    internal MethodComplexityRowViewModel(CodeElement element, MemberMetrics metrics)
    {
        Element = element;
        Name = element.FullName;
        Lines = metrics.LinesOfCode;
        Complexity = metrics.CyclomaticComplexity;
    }

    /// <summary>The underlying method node, used to add it to the Code Explorer.</summary>
    public CodeElement Element { get; }

    public string Name { get; }
    public int Lines { get; }
    public int Complexity { get; }
}
