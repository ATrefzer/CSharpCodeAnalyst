using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Enum member initializers: "enum Level { Highest = Limits.Max }". Enum members are deliberately not
///     code elements (references to them fall back to the enum type), but as a consequence their
///     initializer expressions are never walked in phase 2 - the dependency of the enum on the referenced
///     constant is lost. The edge should be anchored on the enum element.
/// </summary>
[TestFixture]
public class EnumMemberInitializerParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                      namespace Demo;

                                      public static class Limits
                                      {
                                          public const int Max = 100;
                                      }

                                      public enum Level
                                      {
                                          Low = 1,
                                          Highest = Limits.Max
                                      }
                                      """;

    [Test]
    public void EnumAndConstant_AreDetected()
    {
        // Premise guard (green): both sides of the missing edge exist as elements.
        Assert.Multiple(() =>
        {
            Assert.That(PathsOf(CodeElementType.Enum), Does.Contain("Level"));
            Assert.That(PathsOf(CodeElementType.Field), Does.Contain("Limits.Max"));
        });
    }

    [Test]
    public void EnumMemberInitializer_IsDetectedAsUses()
    {
        Assert.That(RelsOf(RelationshipType.Uses), Does.Contain("Level -> Limits.Max"));
    }
}
