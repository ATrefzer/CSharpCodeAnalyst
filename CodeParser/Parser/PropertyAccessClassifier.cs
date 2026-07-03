using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpCodeAnalyst.CodeParser.Parser;

/// <summary>
///     Whether a property reference reads (getter), writes (setter) or does both.
/// </summary>
internal enum PropertyAccessKind
{
    /// <summary>Getter only, e.g. <c>x = Prop;</c></summary>
    Read,

    /// <summary>Setter only, e.g. <c>Prop = x;</c></summary>
    Write,

    /// <summary>Getter and setter, e.g. <c>Prop += 1;</c> or <c>Prop++;</c></summary>
    ReadWrite
}

/// <summary>
///     Classifies a property reference in a syntax tree as a getter access, a setter access or both.
///     This is a purely syntactic decision - it does not need a semantic model.
///
///     The key simplification: C# does not allow a property to be passed by <c>ref</c>/<c>out</c>
///     (CS0206).  Therefore, the only contexts that invoke the setter are
///     <list type="bullet">
///         <item>the target of a (simple or compound) assignment, and</item>
///         <item>the operand of an increment/decrement (<c>++</c>/<c>--</c>).</item>
///     </list>
///     Every other position is a pure getter access. A compound assignment (<c>+=</c>, <c>??=</c>, ...)
///     and increment/decrement read the current value before writing it back, so they are
///     <see cref="PropertyAccessKind.ReadWrite" />.
///
///     Note:
///     The semantic information returns a property without distinguishing between getter and setter. So we have to additionally
///     use the syntax tree to classify the access as a read, write or read/write.
///
///     obj.Prop = x
///     │
///     ├─ GetSymbolInfo  → IPropertySymbol(which Property?)
///     └─ Classify(node) → Write(get or set?)
///     │
///     └─ Lookup propertySymbol.SetMethod.Key()  → Node "Prop.set"
///
/// </summary>
internal static class PropertyAccessClassifier
{
    /// <summary>
    ///     <paramref name="propertyReference" /> is the full expression that refers to the property:
    ///     an <see cref="IdentifierNameSyntax" /> for <c>Prop</c>, a <see cref="MemberAccessExpressionSyntax" />
    ///     for <c>obj.Prop</c> or an <see cref="ElementAccessExpressionSyntax" /> for an indexer <c>obj[i]</c>.
    /// </summary>
    public static PropertyAccessKind Classify(ExpressionSyntax propertyReference)
    {
        // Parentheses are transparent for assignment targeting: (Prop) = x writes Prop.
        var node = AscendPastParentheses(propertyReference);

        switch (node.Parent)
        {
            case AssignmentExpressionSyntax assignment when assignment.Left == node:
                // Simple "=" only writes; compound "+=", "-=", "??=", ... read-modify-write.
                return assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                    ? PropertyAccessKind.Write
                    : PropertyAccessKind.ReadWrite;

            case PrefixUnaryExpressionSyntax prefix when IsIncrementOrDecrement(prefix):
                return PropertyAccessKind.ReadWrite;

            case PostfixUnaryExpressionSyntax postfix when IsIncrementOrDecrement(postfix):
                return PropertyAccessKind.ReadWrite;

            default:
                return PropertyAccessKind.Read;
        }
    }

    private static ExpressionSyntax AscendPastParentheses(ExpressionSyntax node)
    {
        while (node.Parent is ParenthesizedExpressionSyntax parenthesized)
        {
            node = parenthesized;
        }

        return node;
    }

    private static bool IsIncrementOrDecrement(ExpressionSyntax node)
    {
        return node.IsKind(SyntaxKind.PreIncrementExpression)
               || node.IsKind(SyntaxKind.PreDecrementExpression)
               || node.IsKind(SyntaxKind.PostIncrementExpression)
               || node.IsKind(SyntaxKind.PostDecrementExpression);
    }
}
