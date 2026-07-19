using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Partial methods/properties: the definition part and the implementation part are two different
///     symbols with the same key. Phase 1 stores whichever symbol it sees first; phase 2 walks only the
///     stored symbol's DeclaringSyntaxReferences. When the definition part comes first (declaration
///     order!), the implementation body is never analyzed and all its dependencies are lost - order
///     dependent and systematic for source generators, where the user writes the definition part.
/// </summary>
[TestFixture]
public class PartialMemberBodyParseTests : InMemoryParseTestBase
{
    protected override string Code => """
                                      namespace Demo;

                                      public class Item { }
                                      public class Gadget { }

                                      // Definition part first - the implementation body must still be analyzed.
                                      public partial class WorkerDefinitionFirst
                                      {
                                          public partial void Hook();
                                          public void Caller() { Hook(); }
                                      }

                                      public partial class WorkerDefinitionFirst
                                      {
                                          public partial void Hook() { var item = new Item(); }
                                      }

                                      // Implementation part first - control group.
                                      public partial class WorkerImplementationFirst
                                      {
                                          public partial void Hook() { var gadget = new Gadget(); }
                                      }

                                      public partial class WorkerImplementationFirst
                                      {
                                          public partial void Hook();
                                          public void Caller() { Hook(); }
                                      }

                                      // Partial property (C# 13), definition part first.
                                      public partial class Store
                                      {
                                          public partial Item Current { get; }
                                      }

                                      public partial class Store
                                      {
                                          public partial Item Current
                                          {
                                              get { return new Item(); }
                                          }
                                      }
                                      """;

    [Test]
    public void PartialMethod_IsOneElement_AndCallable()
    {
        // Premise guards (green): one Hook element per class, and the call edge to it exists.
        Assert.Multiple(() =>
        {
            Assert.That(PathsOf(CodeElementType.Method).Count(p => p == "WorkerDefinitionFirst.Hook"), Is.EqualTo(1));
            Assert.That(RelsOf(RelationshipType.Calls), Does.Contain("WorkerDefinitionFirst.Caller -> WorkerDefinitionFirst.Hook"));
        });
    }

    [Test]
    public void ImplementationPartFirst_BodyIsAnalyzed()
    {
        // Control group (green): with the implementation part first the body dependencies exist.
        Assert.That(RelsOf(RelationshipType.Creates), Does.Contain("WorkerImplementationFirst.Hook -> Gadget"));
    }

    [Test]
    public void DefinitionPartFirst_BodyIsAnalyzed()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RelsOf(RelationshipType.Creates), Does.Contain("WorkerDefinitionFirst.Hook -> Item"));
            Assert.That(RelsOf(RelationshipType.Uses), Does.Contain("WorkerDefinitionFirst.Hook -> Item"));
        });
    }

    [Test]
    public void PartialProperty_DefinitionPartFirst_BodyIsAnalyzed()
    {
        Assert.That(RelsOf(RelationshipType.Creates), Does.Contain("Store.Current -> Item"));
    }
}
