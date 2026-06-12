namespace ParserGaps.PatternMatching;

// KNOWN GAP: Type references in patterns are not captured.
// "shape is Circle circle" is an IsPatternExpressionSyntax, not the BinaryExpressionSyntax
// handled in SyntaxWalkerBase.VisitBinaryExpression. The type identifier resolves to an
// INamedTypeSymbol, which AnalyzeIdentifier ignores.

public abstract class Shape
{
}

public class Circle : Shape
{
}

public class Square : Shape
{
}

public class Triangle : Shape
{
}

public class Rectangle : Shape
{
}

public class PatternUser
{
    // GAP: no Uses relationship DeclarationPattern -> Circle.
    public int DeclarationPattern(Shape shape)
    {
        if (shape is Circle circle)
        {
            return circle == null ? 0 : 1;
        }

        return 0;
    }

    // GAP: no Uses relationships SwitchExpression -> Square / Triangle.
    public int SwitchExpression(Shape shape)
    {
        return shape switch
        {
            Square => 2,
            Triangle => 3,
            _ => 0
        };
    }

    // GAP: no Uses relationship CaseStatement -> Rectangle.
    public int CaseStatement(Shape shape)
    {
        switch (shape)
        {
            case Rectangle rectangle:
                return rectangle == null ? 0 : 4;
            default:
                return 0;
        }
    }
}
