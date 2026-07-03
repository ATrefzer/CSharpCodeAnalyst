using CodeGraph.Metrics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeParser.Parser;

/// <summary>
///     Computes source-level metrics (lines of code, cyclomatic complexity) for a member from its
///     declaration syntax. Deliberately hand-rolled and small: we control exactly what is counted,
///     and we avoid the awkward MSBuild-task / internal-library packaging of the Microsoft metrics.
/// </summary>
public static class SourceMetricsCollector
{
    public static MemberMetrics Compute(SyntaxNode declaration)
    {
        return new MemberMetrics
        {
            LinesOfCode = CountLines(declaration),
            CyclomaticComplexity = ComputeCyclomaticComplexity(declaration)
        };
    }

    private static int CountLines(SyntaxNode node)
    {
        var span = node.GetLocation().GetLineSpan();
        return span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
    }

    /// <summary>
    ///     McCabe cyclomatic complexity: one plus the number of decision points. The exact set of
    ///     decision points varies between tools; we count branching statements, switch cases, catch
    ///     clauses, the conditional operator and the short-circuiting / null-coalescing operators.
    /// </summary>
    private static int ComputeCyclomaticComplexity(SyntaxNode node)
    {
        return 1 + node.DescendantNodes().Count(IsDecisionPoint);
    }

    private static bool IsDecisionPoint(SyntaxNode node)
    {
        switch (node)
        {
            case IfStatementSyntax:
            case WhileStatementSyntax:
            case DoStatementSyntax:
            case ForStatementSyntax:
            case ForEachStatementSyntax:
            case CaseSwitchLabelSyntax:
            case CasePatternSwitchLabelSyntax:
            case SwitchExpressionArmSyntax:
            case CatchClauseSyntax:
            case ConditionalExpressionSyntax:
                return true;
            case BinaryExpressionSyntax binary:
                return binary.IsKind(SyntaxKind.LogicalAndExpression)
                       || binary.IsKind(SyntaxKind.LogicalOrExpression)
                       || binary.IsKind(SyntaxKind.CoalesceExpression);
            default:
                return false;
        }
    }
}
