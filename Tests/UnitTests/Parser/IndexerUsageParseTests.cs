using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Usage of an indexer (element access, "store[key]"). The indexer declaration is a code element
///     ("this[]") and its body is analyzed, but element access expressions (ElementAccessExpressionSyntax /
///     conditional ElementBindingExpressionSyntax) are currently not visited at all - no caller ever gets
///     an edge to the indexer, so internal indexers always look unused.
/// </summary>
[TestFixture]
public class IndexerUsageParseTests : InMemoryParseTestBase
{
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

                                          public int? ConditionalRead(Store? store)
                                          {
                                              return store?[4];
                                          }
                                      }
                                      """;

    [Test]
    public void IndexerElement_IsDetected()
    {
        // Premise guard (green): the declaration side works, only the usage side is missing.
        Assert.That(PathsOf(CodeElementType.Property), Does.Contain("Store.this[]"));
    }

    [Test]
    public void IndexerRead_IsDetected()
    {
        // Accessors are always split, so a read targets the getter, not the indexer container.
        Assert.That(RelsOf(RelationshipType.Calls), Does.Contain("Client.Read -> Store.this[].get_Item"));
    }

    [Test]
    public void IndexerWrite_IsDetected()
    {
        Assert.That(RelsOf(RelationshipType.Calls), Does.Contain("Client.Write -> Store.this[].set_Item"));
    }

    [Test]
    public void IndexerCompoundAssignment_IsDetected()
    {
        // "store[3] += 1" both reads and writes - both accessors get the edge.
        Assert.Multiple(() =>
        {
            Assert.That(RelsOf(RelationshipType.Calls), Does.Contain("Client.Increment -> Store.this[].get_Item"));
            Assert.That(RelsOf(RelationshipType.Calls), Does.Contain("Client.Increment -> Store.this[].set_Item"));
        });
    }

    [Test]
    public void ConditionalIndexerAccess_IsDetected()
    {
        Assert.That(RelsOf(RelationshipType.Calls), Does.Contain("Client.ConditionalRead -> Store.this[].get_Item"));
    }
}
