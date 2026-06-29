using CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Type references inside patterns (declaration / type / recursive patterns, switch expressions and
///     case labels) are captured as Uses. Migrated from the former Core.BasicLanguageFeatures approval
///     fixture (PatternMatching namespace).
/// </summary>
[TestFixture]
public class BasicLanguageFeatures_PatternMatchingParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                namespace Demo;

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
                                    public int DeclarationPattern(Shape shape)
                                    {
                                        if (shape is Circle circle)
                                        {
                                            return circle == null ? 0 : 1;
                                        }

                                        return 0;
                                    }

                                    public int SwitchExpression(Shape shape)
                                    {
                                        return shape switch
                                        {
                                            Square => 2,
                                            Triangle => 3,
                                            _ => 0
                                        };
                                    }

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
                                """;

    [Test]
    public void Classes_AreDetected()
    {
        var expected = new[] { "Shape", "Circle", "Square", "Triangle", "Rectangle", "PatternUser" };
        Assert.That(PathsOf(CodeElementType.Class), Is.EquivalentTo(expected));
    }

    [Test]
    public void PatternTypeReferences_AreDetectedAsUses()
    {
        var expected = new[]
        {
            "PatternUser.DeclarationPattern -> Shape",
            "PatternUser.DeclarationPattern -> Circle",
            "PatternUser.SwitchExpression -> Shape",
            "PatternUser.SwitchExpression -> Square",
            "PatternUser.SwitchExpression -> Triangle",
            "PatternUser.CaseStatement -> Shape",
            "PatternUser.CaseStatement -> Rectangle"
        };

        Assert.That(RelsOf(RelationshipType.Uses), Is.EquivalentTo(expected));
    }
}
