using CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Type names in catch declarations, foreach variable types, using-statement declarations, array
///     creation and throw expressions are captured (Uses / Creates). Migrated from the former
///     Core.BasicLanguageFeatures approval fixture (TypeContexts namespace).
/// </summary>
[TestFixture]
public class BasicLanguageFeatures_TypeContextsParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                namespace Demo;

                                public class ParsingFailedException : System.Exception
                                {
                                }

                                public class InventoryItem
                                {
                                }

                                public class PooledResource : System.IDisposable
                                {
                                    public void Dispose()
                                    {
                                    }
                                }

                                public class TypeContextUser
                                {
                                    private readonly InventoryItem[] _items = [];

                                    public int CatchClause()
                                    {
                                        try
                                        {
                                            return Work();
                                        }
                                        catch (ParsingFailedException)
                                        {
                                            return 0;
                                        }
                                    }

                                    public void Throwing()
                                    {
                                        throw new ParsingFailedException();
                                    }

                                    public int ForEachLoop()
                                    {
                                        var sum = 0;
                                        foreach (InventoryItem item in _items)
                                        {
                                            sum++;
                                        }

                                        return sum;
                                    }

                                    public object ArrayCreation()
                                    {
                                        return new InventoryItem[5];
                                    }

                                    public void UsingStatement()
                                    {
                                        using (PooledResource resource = CreateResource())
                                        {
                                        }
                                    }

                                    private PooledResource CreateResource()
                                    {
                                        return new PooledResource();
                                    }

                                    private int Work()
                                    {
                                        return 1;
                                    }
                                }
                                """;

    [Test]
    public void Classes_AreDetected()
    {
        var expected = new[] { "ParsingFailedException", "InventoryItem", "PooledResource", "TypeContextUser" };
        Assert.That(PathsOf(CodeElementType.Class), Is.EquivalentTo(expected));
    }

    [Test]
    public void ObjectCreations_AreDetectedAsCreates()
    {
        var expected = new[]
        {
            "TypeContextUser.CreateResource -> PooledResource",
            "TypeContextUser.Throwing -> ParsingFailedException"
        };

        Assert.That(RelsOf(RelationshipType.Creates), Is.EquivalentTo(expected));
    }

    [Test]
    public void TypeReferences_AreDetectedAsUses()
    {
        var expected = new[]
        {
            "TypeContextUser._items -> InventoryItem",
            "TypeContextUser.CatchClause -> ParsingFailedException",
            "TypeContextUser.ForEachLoop -> InventoryItem",
            "TypeContextUser.ForEachLoop -> TypeContextUser._items",
            "TypeContextUser.ArrayCreation -> InventoryItem",
            "TypeContextUser.UsingStatement -> PooledResource",
            "TypeContextUser.CreateResource -> PooledResource"
        };

        Assert.That(RelsOf(RelationshipType.Uses), Is.EquivalentTo(expected));
    }

    [Test]
    public void MethodCalls_AreDetected()
    {
        var expected = new[]
        {
            "TypeContextUser.CatchClause -> TypeContextUser.Work",
            "TypeContextUser.UsingStatement -> TypeContextUser.CreateResource"
        };

        Assert.That(RelsOf(RelationshipType.Calls), Is.EquivalentTo(expected));
    }
}
