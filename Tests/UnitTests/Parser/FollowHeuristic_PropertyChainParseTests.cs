namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     FollowIncomingCallsHeuristically: the incoming call chain passes through the property getter
///     Facade.Value; the traversal must continue at the property and reach Client.Consume as the origin.
///     Migrated from the former CodeExplorerApprovalTests scenario (FollowHeuristic/PropertyChain.cs).
/// </summary>
[TestFixture]
public class FollowHeuristic_PropertyChainParseTests : InMemoryFollowIncomingCallsTestBase
{
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
    public void ChainContinuesThroughPropertyGetter()
    {
        var result = FollowIncomingCalls("Repository.Compute");

        Assert.That(RelationshipsOf(result), Is.EquivalentTo(new[]
        {
            "Facade.Value -(Calls)-> Repository.Compute",
            "Client.Consume -(Calls)-> Facade.Value"
        }));

        Assert.That(ElementsOf(result), Is.EquivalentTo(new[]
        {
            "Repository.Compute", "Facade.Value", "Client.Consume"
        }));
    }
}
