using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Property and field initializers: the containing type "creates" the object, the member "uses" it.
///     Migrated from the former Core.BasicLanguageFeatures approval fixture (Initializers namespace).
/// </summary>
[TestFixture]
public class BasicLanguageFeatures_InitializersParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                namespace Demo;

                                public class Engine
                                {
                                }

                                public class CarWithPropertyInitializer
                                {
                                    public Engine Engine { get; } = new Engine();
                                }

                                public class CarWithFieldInitializer
                                {
                                    private readonly Engine _engine = new Engine();
                                }
                                """;

    [Test]
    public void Classes_AreDetected()
    {
        var expected = new[] { "Engine", "CarWithPropertyInitializer", "CarWithFieldInitializer" };
        Assert.That(PathsOf(CodeElementType.Class), Is.EquivalentTo(expected));
    }

    [Test]
    public void Properties_AreDetected()
    {
        Assert.That(PathsOf(CodeElementType.Property), Is.EquivalentTo(new[] { "CarWithPropertyInitializer.Engine" }));
    }

    [Test]
    public void ObjectCreations_AreDetectedAsCreates()
    {
        var expected = new[]
        {
            "CarWithFieldInitializer -> Engine",
            "CarWithPropertyInitializer -> Engine"
        };

        Assert.That(RelsOf(RelationshipType.Creates), Is.EquivalentTo(expected));
    }

    [Test]
    public void MemberTypes_AreDetectedAsUses()
    {
        var expected = new[]
        {
            "CarWithFieldInitializer._engine -> Engine",
            "CarWithPropertyInitializer.Engine -> Engine"
        };

        Assert.That(RelsOf(RelationshipType.Uses), Is.EquivalentTo(expected));
    }
}
