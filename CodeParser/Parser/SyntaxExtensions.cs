using CodeGraph.Graph;
using Microsoft.CodeAnalysis;

namespace CodeParser.Parser;

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
}