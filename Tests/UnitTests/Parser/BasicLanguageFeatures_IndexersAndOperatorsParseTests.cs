using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Indexers, user-defined operators, conversion operators and finalizers are code elements whose
///     bodies are walked in phase 2 (symbol names this[], op_Addition, op_Implicit, Finalize). Migrated
///     from the former Core.BasicLanguageFeatures approval fixture (IndexersAndOperators namespace).
/// </summary>
[TestFixture]
public class BasicLanguageFeatures_IndexersAndOperatorsParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                namespace Demo;

                                public class DataStore
                                {
                                    public int Compute(string key)
                                    {
                                        return key.Length;
                                    }
                                }

                                public class Catalog
                                {
                                    private readonly DataStore _store = new DataStore();

                                    public int Count { get; set; }

                                    public int this[string key]
                                    {
                                        get { return _store.Compute(key); }
                                    }

                                    public static Catalog operator +(Catalog left, Catalog right)
                                    {
                                        var merged = new Catalog();
                                        merged.Absorb(left);
                                        merged.Absorb(right);
                                        return merged;
                                    }

                                    public static implicit operator int(Catalog catalog)
                                    {
                                        return catalog.ComputeTotal();
                                    }

                                    ~Catalog()
                                    {
                                        Cleanup();
                                    }

                                    public void Absorb(Catalog other)
                                    {
                                        Count = Count + other.Count;
                                    }

                                    private int ComputeTotal()
                                    {
                                        return Count;
                                    }

                                    private void Cleanup()
                                    {
                                    }
                                }
                                """;

    [Test]
    public void Classes_AreDetected()
    {
        Assert.That(PathsOf(CodeElementType.Class), Is.EquivalentTo(new[] { "DataStore", "Catalog" }));
    }

    [Test]
    public void Properties_AreDetected()
    {
        Assert.That(PathsOf(CodeElementType.Property), Is.EquivalentTo(new[] { "Catalog.Count", "Catalog.this[]" }));
    }

    [Test]
    public void ObjectCreations_AreDetectedAsCreates()
    {
        var expected = new[]
        {
            "Catalog -> DataStore",
            "Catalog.op_Addition -> Catalog"
        };

        Assert.That(RelsOf(RelationshipType.Creates), Is.EquivalentTo(expected));
    }

    [Test]
    public void TypeReferences_AreDetectedAsUses()
    {
        var expected = new[]
        {
            "Catalog._store -> DataStore",
            "Catalog.Absorb -> Catalog",
            "Catalog.op_Addition -> Catalog",
            "Catalog.op_Implicit -> Catalog",
            "Catalog.this[] -> Catalog._store"
        };

        Assert.That(RelsOf(RelationshipType.Uses), Is.EquivalentTo(expected));
    }

    [Test]
    public void CallsFromSpecialMemberBodies_AreDetected()
    {
        var expected = new[]
        {
            "Catalog.Absorb -> Catalog.Count",
            "Catalog.ComputeTotal -> Catalog.Count",
            "Catalog.Finalize -> Catalog.Cleanup",
            "Catalog.op_Addition -> Catalog.Absorb",
            "Catalog.op_Implicit -> Catalog.ComputeTotal",
            "Catalog.this[] -> DataStore.Compute"
        };

        Assert.That(RelsOf(RelationshipType.Calls), Is.EquivalentTo(expected));
    }
}
