using CodeGraph.Metrics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeParser.Parser;

/// <summary>
///     Computes source-level metrics for a member from its declaration syntax. Deliberately
///     hand-rolled and small (syntax tree only, no semantic model): we control exactly what is
///     counted and avoid the awkward packaging of the Microsoft code-metrics engine.
/// </summary>
public static class SourceMetricsCollector
{
    public static MemberMetrics Compute(SyntaxNode declaration)
    {
        var codeLines = new HashSet<int>();
        var commentLines = new HashSet<int>();

        // A line is "code" when a real token touches it.
        foreach (var token in declaration.DescendantTokens())
        {
            AddLines(codeLines, token.GetLocation().GetLineSpan());
        }

        // Comment trivia inside the member, plus the documentation / comment block directly above the
        // signature (which lives in the first token's leading trivia and is part of DescendantTrivia).
        foreach (var trivia in declaration.DescendantTrivia())
        {
            if (IsComment(trivia.Kind()))
            {
                AddLines(commentLines, trivia.GetLocation().GetLineSpan());
            }
        }

        // A line with code stays code even if it also carries a trailing comment.
        commentLines.ExceptWith(codeLines);

        return new MemberMetrics
        {
            CodeLines = codeLines.Count,
            CommentLines = commentLines.Count,
            LogicalLinesOfCode = CountLogicalLines(declaration),
            CyclomaticComplexity = ComputeCyclomaticComplexity(declaration)
        };
    }

    private static void AddLines(HashSet<int> lines, FileLinePositionSpan span)
    {
        for (var line = span.StartLinePosition.Line; line <= span.EndLinePosition.Line; line++)
        {
            lines.Add(line);
        }
    }

    /// <summary>
    ///     Number of executable statements. Block statements (the wrapping braces) are not counted;
    ///     an expression-bodied member (no statements) counts as a single logical line.
    /// </summary>
    private static int CountLogicalLines(SyntaxNode node)
    {
        var statements = node.DescendantNodes().OfType<StatementSyntax>().Count(s => s is not BlockSyntax);
        if (statements == 0 && node.DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Any())
        {
            return 1;
        }

        return statements;
    }

    /// <summary>
    ///     McCabe cyclomatic complexity: one plus the number of decision points (branching statements,
    ///     switch cases, catch clauses, the conditional operator and the short-circuiting / null
    ///     coalescing operators). The exact set varies between tools.
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

    private static bool IsComment(SyntaxKind kind)
    {
        return kind is SyntaxKind.SingleLineCommentTrivia or SyntaxKind.MultiLineCommentTrivia
            or SyntaxKind.SingleLineDocumentationCommentTrivia or SyntaxKind.MultiLineDocumentationCommentTrivia;
    }
}
