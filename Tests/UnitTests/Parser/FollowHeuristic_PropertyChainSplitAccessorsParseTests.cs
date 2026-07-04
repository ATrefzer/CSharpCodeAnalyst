using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     The PropertyChain scenario parsed with SplitPropertyAccessors enabled: a getter-only property is
///     split into a single get_ accessor child, the accessor body (not the property container) carries the
///     outgoing call, a read access targets the getter, and FollowIncomingCallsHeuristically traverses
///     through the getter accessor to reach the reader. Migrated from the FollowHeuristic.PropertyChain
///     parts of the former SplitPropertyAccessorsTests (which parsed the whole TestSuite solution).
/// </summary>
[TestFixture]
public class FollowHeuristic_PropertyChainSplitAccessorsParseTests : InMemoryFollowIncomingCallsTestBase
{
    protected override bool SplitPropertyAccessors => true;

    protected override string Code => """
                                     namespace FollowHeuristic.PropertyChain;

                                     public class Repository
                                     {
                                         public int Compute() { return 42; }
                                     }

                                     public class Facade
                                     {
                                         private readonly Repository _repository = new();
                                         public int Value
                                         {
                                             get { return _repository.Compute(); }
                                         }
                                     }

                                     public class Client
                                     {
                                         public void Consume()
                                         {
                                             var facade = new Facade();
                                             var unused = facade.Value;
                                         }
                                     }
                                     """;

    [Test]
    public void GetterOnlyProperty_HasSingleGetterChild()
    {
        var accessorChildren = Node("Facade.Value").Children
            .Where(c => c.ElementType == CodeElementType.PropertyAccessor)
            .Select(c => c.Name);

        Assert.That(accessorChildren, Is.EquivalentTo(new[] { "get_Value" }));
    }

    [Test]
    public void AccessorBody_IsAttributedToAccessor_NotToPropertyContainer()
    {
        Assert.That(HasCall("Facade.Value.get_Value", "Repository.Compute"), Is.True,
            "Getter body should call Repository.Compute.");
        Assert.That(HasCall("Facade.Value", "Repository.Compute"), Is.False,
            "The property container should not carry the accessor's call.");
    }

    [Test]
    public void ReadAccess_TargetsGetter_NotPropertyContainer()
    {
        Assert.That(HasCall("Client.Consume", "Facade.Value.get_Value"), Is.True,
            "Reading the property should target the getter element.");
        Assert.That(HasCall("Client.Consume", "Facade.Value"), Is.False,
            "Reading the property should no longer target the property container.");
    }

    [Test]
    public void FollowIncomingCalls_TraversesThroughAccessor()
    {
        var result = FollowIncomingCalls("Repository.Compute");

        Assert.That(ElementsOf(result), Does.Contain("Facade.Value.get_Value"),
            "Traversal should pass through the getter accessor.");
        Assert.That(ElementsOf(result), Does.Contain("Client.Consume"),
            "Traversal should reach the reader Client.Consume through the accessor.");
    }

    private CodeElement Node(string path)
    {
        return Graph.Nodes.Values.Single(n => PathOf(n) == path);
    }

    private bool HasCall(string sourcePath, string targetPath)
    {
        return Node(sourcePath).Relationships
            .Any(r => r.Type == RelationshipType.Calls && PathOf(Graph.Nodes[r.TargetId]) == targetPath);
    }
}
