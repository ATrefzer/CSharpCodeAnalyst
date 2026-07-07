using CSharpCodeAnalyst.CodeGraph.Metrics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpCodeAnalyst.CodeParser.Parser;

/// <summary>
///     Computes source-level metrics for a member from its declaration syntax. Deliberately
///     hand-rolled and small (syntax tree only, no semantic model): we control exactly what is
///     counted and avoid the awkward packaging of the Microsoft code-metrics engine.
/// </summary>
public static class SourceMetricsCollector
{
    /// <summary>
    ///     Whether the declaration has an implementation to measure (a block or expression body).
    ///     False for members without one: abstract/extern/interface method declarations, and the
    ///     signature-only half of a partial method/property.
    /// </summary>
    public static bool HasBody(SyntaxNode declaration)
    {
        return declaration.ChildNodes().Any(n => n is BlockSyntax or ArrowExpressionClauseSyntax);
    }

    public static (int Code, int Comment) ComputeForFile(string pathToCSharpFile)
    {
        var text = File.ReadAllText(pathToCSharpFile);
        var tree = CSharpSyntaxTree.ParseText(text, path: pathToCSharpFile);
        var root = tree.GetRoot();
        var stats = ComputeFromSyntaxNode(root);
        return (stats.codeLines.Count, stats.commentLines.Count);
    }

    public static MemberMetrics ComputeForMethodDeclaration(SyntaxNode declaration)
    {
        var (codeLines, commentLines) = ComputeFromSyntaxNode(declaration);

        return new MemberMetrics
        {
            CodeLines = codeLines.Count,
            CommentLines = commentLines.Count,
            LogicalLinesOfCode = CountLogicalLines(declaration),
            CyclomaticComplexity = ComputeCyclomaticComplexity(declaration)
        };
    }

    /// <summary>
    /// The given syntaxNode may be whole syntax tree of a C# file or a method declaration to calculate the metrics for a
    /// method only.
    /// </summary>
    private static (HashSet<int> codeLines, HashSet<int> commentLines) ComputeFromSyntaxNode(SyntaxNode syntaxNode)
    {
        var codeLines = new HashSet<int>();
        var commentLines = new HashSet<int>();

        // A line is "code" when a real token touches it. Method signature and curly standalone curly brackets count.
        // There is no separate "blank" concept: every line in a token's/trivia's line span is
        // attributed to Code/Comment, even if that line is empty. A multi-line raw or verbatim
        // string literal is a single token, so a blank-looking line inside it still counts as
        // Code here - unlike LinesOfCodeProvider (the text-scanning counter for the same
        // purpose), which always counts a blank-looking line as blank regardless of context.
        // This is the main source of the small per-file differences between the two counters
        // and is accepted, not a bug - see ArchitecturalRules/Analyzer.cs GetSampleRules() for a
        // real example (6 blank lines inside a raw string literal).
        foreach (var token in syntaxNode.DescendantTokens())
        {
            if (token.Kind() == SyntaxKind.EndOfFileToken)
            {
                // We would overcount to 1 for an empty file.
                continue;
            }
            AddLines(codeLines, token.GetLocation().GetLineSpan());
        }

        // Comment trivia inside the member, plus the documentation / comment block directly above the
        // signature (which lives in the first token's leading trivia and is part of DescendantTrivia).
        foreach (var trivia in syntaxNode.DescendantTrivia())
        {
            if (IsComment(trivia.Kind()))
            {
                AddLines(commentLines, trivia.GetLocation().GetLineSpan());
            }
        }

        // A line with code stays code even if it also carries a trailing comment.
        commentLines.ExceptWith(codeLines);
        return (codeLines, commentLines);
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
    ///     We count the outer statement nodes, not the inner expressions, so "if (x) { y(); }" counts as one logical line, not two.
    /// </summary>
    private static int CountLogicalLines(SyntaxNode node)
    {
        // Note: => x * 2;, without { } has no StatementSyntax node.
        var statements = node.DescendantNodes().OfType<StatementSyntax>().Count(s => s is not BlockSyntax);
        if (statements == 0 && node.DescendantNodes().OfType<ArrowExpressionClauseSyntax>().Any())
        {
            return 1;
        }

        return statements;
    }

    /// <summary>
    ///     McCabe cyclomatic complexity: one plus the number of decision points.
    ///     The exact set varies between tools.
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
            case CatchClauseSyntax:
            case ConditionalExpressionSyntax:
            case BinaryPatternSyntax: // "and" / "or" pattern combinators, e.g. "case int or string".
                return true;
            case SwitchExpressionArmSyntax arm:
                // A bare "_ => ..." catch-all arm is the switch-expression equivalent of a classic
                // "default:" label, which is also not counted. A guarded discard ("_ when ...") is a
                // real condition and does count.
                return arm.Pattern is not DiscardPatternSyntax || arm.WhenClause is not null;
            case BinaryExpressionSyntax binary:
                return binary.IsKind(SyntaxKind.LogicalAndExpression)
                       || binary.IsKind(SyntaxKind.LogicalOrExpression)
                       || binary.IsKind(SyntaxKind.CoalesceExpression);
            case AssignmentExpressionSyntax assignment:
                // "x ??= y" carries the same branch as "x = x ?? y", just through a different node type.
                return assignment.IsKind(SyntaxKind.CoalesceAssignmentExpression);
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
