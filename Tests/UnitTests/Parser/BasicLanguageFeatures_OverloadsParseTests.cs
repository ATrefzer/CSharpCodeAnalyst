using CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Overloads that differ only by parameter ref-kind, and overloaded indexers (same FullName this[],
///     distinguished by parameter type), must each become a distinct code element whose body is walked.
///     Before the Key() fix the second overload was dropped and one of the edges below went missing.
///     Migrated from the former Core.BasicLanguageFeatures approval fixture (Overloads namespace).
/// </summary>
[TestFixture]
public class BasicLanguageFeatures_OverloadsParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                namespace Demo;

                                public class ByValueResult
                                {
                                }

                                public class ByRefResult
                                {
                                }

                                public class ByOutResult
                                {
                                }

                                public class Calculator
                                {
                                    public void Compute(int value)
                                    {
                                        var result = new ByValueResult();
                                    }

                                    public void Compute(ref int value)
                                    {
                                        var result = new ByRefResult();
                                    }

                                    public void Compute(out int value)
                                    {
                                        value = 0;
                                        var result = new ByOutResult();
                                    }
                                }

                                public class IntStore
                                {
                                }

                                public class TextStore
                                {
                                }

                                public class Repository
                                {
                                    private readonly IntStore _byIndex = new IntStore();
                                    private readonly TextStore _byKey = new TextStore();

                                    public IntStore this[int index]
                                    {
                                        get { return _byIndex; }
                                    }

                                    public TextStore this[string key]
                                    {
                                        get { return _byKey; }
                                    }
                                }
                                """;

    [Test]
    public void Classes_AreDetected()
    {
        var expected = new[]
        {
            "ByValueResult", "ByRefResult", "ByOutResult", "Calculator", "IntStore", "TextStore", "Repository"
        };

        Assert.That(PathsOf(CodeElementType.Class), Is.EquivalentTo(expected));
    }

    [Test]
    public void Properties_AreDetected()
    {
        // Both overloaded indexers share the FullName this[]; they are distinct elements via Key().
        Assert.That(PathsOf(CodeElementType.Property), Is.EquivalentTo(new[] { "Repository.this[]", "Repository.this[]" }));
    }

    [Test]
    public void ObjectCreations_AreDetectedAsCreates()
    {
        var expected = new[]
        {
            // Each ref-kind overload body creates its own type (all three overloads are walked).
            "Calculator.Compute -> ByValueResult",
            "Calculator.Compute -> ByRefResult",
            "Calculator.Compute -> ByOutResult",

            // Both indexer field initializers (attributed to the type).
            "Repository -> IntStore",
            "Repository -> TextStore"
        };

        Assert.That(RelsOf(RelationshipType.Creates), Is.EquivalentTo(expected));
    }

    [Test]
    public void TypeReferences_AreDetectedAsUses()
    {
        var expected = new[]
        {
            "Calculator.Compute -> ByValueResult",
            "Calculator.Compute -> ByRefResult",
            "Calculator.Compute -> ByOutResult",

            // Field types, indexer return types and indexer body field reads from both indexers.
            "Repository._byIndex -> IntStore",
            "Repository._byKey -> TextStore",
            "Repository.this[] -> IntStore",
            "Repository.this[] -> TextStore",
            "Repository.this[] -> Repository._byIndex",
            "Repository.this[] -> Repository._byKey"
        };

        Assert.That(RelsOf(RelationshipType.Uses), Is.EquivalentTo(expected));
    }
}
