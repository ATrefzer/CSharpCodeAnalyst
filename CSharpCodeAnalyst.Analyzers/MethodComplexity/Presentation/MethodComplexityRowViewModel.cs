using System.Globalization;
using CSharpCodeAnalyst.AnalyzerSdk.DynamicDataGrid.Contracts.TabularData;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeGraph.Metrics;

namespace CSharpCodeAnalyst.Analyzers.MethodComplexity.Presentation;

public class MethodComplexityRowViewModel : TableRow
{
    internal MethodComplexityRowViewModel(CodeElement element, MemberMetrics metrics)
    {
        Element = element;
        Name = element.FullName;
        Code = metrics.CodeLines;
        Statements = metrics.LogicalLinesOfCode;
        Comments = metrics.CommentLines;
        Complexity = metrics.CyclomaticComplexity;

        var documented = metrics.CodeLines + metrics.CommentLines;
        CommentRatioValue = documented == 0 ? 0.0 : (double)metrics.CommentLines / documented;
        CommentRatio = CommentRatioValue.ToString("P0", CultureInfo.InvariantCulture);
    }

    /// <summary>The underlying method node, used to add it to the Code Explorer.</summary>
    public CodeElement Element { get; }

    public string Name { get; }
    public int Code { get; }
    public int Statements { get; }
    public int Comments { get; }
    public string CommentRatio { get; }
    public double CommentRatioValue { get; }
    public int Complexity { get; }
}
