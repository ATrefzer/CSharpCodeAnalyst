using CSharpCodeAnalyst.CodeGraph.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpCodeAnalyst.CodeParser.Parser;

internal static class SyntaxExtensions
{
    /// <summary>
    ///     Get the source location of a syntax node
    /// </summary>
    public static SourceLocation GetSyntaxLocation(this SyntaxNode node)
    {
        var location = new SourceLocation(
            node.SyntaxTree.FilePath,
            node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            node.GetLocation().GetLineSpan().StartLinePosition.Character + 1);
        return location;
    }

    /// <summary>
    ///     True when the node appears inside a <c>nameof(...)</c> argument. nameof is a compile-time
    ///     construct: the referenced member is not read, written or invoked at run time - the code only
    ///     depends on the symbol's name. The path from the referenced name up to the enclosing nameof
    ///     invocation can only run through member access (qualified names), the argument and the argument
    ///     list, so we walk exactly those. The null-symbol check rules out the pathological case of a real
    ///     method literally named "nameof".
    ///
    ///     InvocationExpressionSyntax           "nameof(Prop)"
    ///     ├─ Expression:  IdentifierNameSyntax "nameof"
    ///     └─ ArgumentListSyntax                "(Prop)"
    ///         └─ ArgumentSyntax                 "Prop"
    ///            └─ IdentifierNameSyntax        "Prop"   ← node
    ///
    ///     InvocationExpressionSyntax
    ///     ├─ Expression:  IdentifierNameSyntax "nameof"
    ///     └─ ArgumentListSyntax
    ///         └─ ArgumentSyntax
    ///             └─ MemberAccessExpressionSyntax  "this.Prop"   ← node
    ///
    /// </summary>
    public static bool IsInsideNameOf(this SyntaxNode node, SemanticModel semanticModel)
    {
        var current = node.Parent;
        while (current is MemberAccessExpressionSyntax or ArgumentSyntax or ArgumentListSyntax)
        {
            current = current.Parent;
        }

        return current is InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "nameof" } } invocation
               && semanticModel.GetSymbolInfo(invocation).Symbol is null;
    }
}