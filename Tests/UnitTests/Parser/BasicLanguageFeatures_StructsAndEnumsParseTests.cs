using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Struct and enum declarations are detected as their own element types. Migrated from the former
///     Core.BasicLanguageFeatures approval fixture (Point / Rectangle structs, Color / Priority enums).
/// </summary>
[TestFixture]
public class BasicLanguageFeatures_StructsAndEnumsParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                namespace Demo;

                                public struct Point
                                {
                                }

                                public struct Rectangle
                                {
                                }

                                public enum Color
                                {
                                    Red,
                                    Green,
                                    Blue
                                }

                                public enum Priority
                                {
                                    Low = 1,
                                    Medium = 5,
                                    High = 10
                                }
                                """;

    [Test]
    public void Structs_AreDetected()
    {
        Assert.That(PathsOf(CodeElementType.Struct), Is.EquivalentTo(new[] { "Point", "Rectangle" }));
    }

    [Test]
    public void Enums_AreDetected()
    {
        Assert.That(PathsOf(CodeElementType.Enum), Is.EquivalentTo(new[] { "Color", "Priority" }));
    }
}
