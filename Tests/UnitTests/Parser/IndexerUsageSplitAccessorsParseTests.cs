using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Indexer usage with SplitPropertyAccessors enabled: a read must be routed to the getter, a write to
///     the setter, a compound assignment to both - exactly like normal property accesses.
///     PropertyAccessClassifier already documents ElementAccessExpressionSyntax as an expected input, but
///     element access is never wired into the walkers. Note: Roslyn names indexer accessors after the
///     metadata name, i.e. get_Item / set_Item (not get_this[]).
/// </summary>
[TestFixture]
public class IndexerUsageSplitAccessorsParseTests : InMemoryParseTestBase
{
    protected override bool SplitPropertyAccessors => true;

    protected override string Code => """
                                      namespace Demo;

                                      public class Store
                                      {
                                          private readonly int[] _data = new int[10];

                                          public int this[int index]
                                          {
                                              get { return _data[index]; }
                                              set { _data[index] = value; }
                                          }
                                      }

                                      public class Client
                                      {
                                          public int Read(Store store)
                                          {
                                              return store[1];
                                          }

                                          public void Write(Store store)
                                          {
                                              store[2] = 42;
                                          }

                                          public void Increment(Store store)
                                          {
                                              store[3] += 1;
                                          }
                                      }
                                      """;

    [Test]
    public void IndexerAccessorElements_AreCreated()
    {
        // Premise guard (green): phase 1 creates the accessor children for indexers.
        Assert.That(PathsOf(CodeElementType.PropertyAccessor),
            Is.EquivalentTo(new[] { "Store.this[].get_Item", "Store.this[].set_Item" }));
    }

    [Test]
    public void IndexerRead_TargetsGetter()
    {
        Assert.That(RelsOf(RelationshipType.Calls), Does.Contain("Client.Read -> Store.this[].get_Item"));
    }

    [Test]
    public void IndexerWrite_TargetsSetter()
    {
        Assert.That(RelsOf(RelationshipType.Calls), Does.Contain("Client.Write -> Store.this[].set_Item"));
    }

    [Test]
    public void IndexerCompoundAssignment_TargetsBothAccessors()
    {
        var calls = RelsOf(RelationshipType.Calls);
        Assert.Multiple(() =>
        {
            Assert.That(calls, Does.Contain("Client.Increment -> Store.this[].get_Item"));
            Assert.That(calls, Does.Contain("Client.Increment -> Store.this[].set_Item"));
        });
    }
}
