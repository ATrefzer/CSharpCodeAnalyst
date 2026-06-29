namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     FollowIncomingCallsHeuristically: same shape as <see cref="FollowHeuristic_BaseInstanceCallParseTests" />
///     but Base.AddToSlave has no <c>_base</c> instance call, so the context is never reset. The hierarchy
///     restriction holds and the sibling ViewModelAdapter2 is correctly excluded. Migrated from the former
///     CodeExplorerApprovalTests scenario (Old.CSharpLanguage/Regression_FollowIncomingCalls2).
/// </summary>
[TestFixture]
public class FollowHeuristic_NoBaseInstanceCallParseTests : InMemoryFollowIncomingCallsTestBase
{
    protected override string Code => """
                                     namespace Demo;

                                     internal abstract class Base
                                     {
                                         protected virtual void AddToSlave()
                                         {
                                         }

                                         public void Build()
                                         {
                                             AddToSlave();
                                         }
                                     }

                                     internal class ViewModelAdapter1 : Base
                                     {
                                         protected override void AddToSlave()
                                         {
                                             base.AddToSlave();
                                         }
                                     }

                                     internal class ViewModelAdapter2 : Base
                                     {
                                         protected override void AddToSlave()
                                         {
                                             base.AddToSlave();
                                         }
                                     }

                                     internal class Driver
                                     {
                                         private readonly ViewModelAdapter1 _adpater1 = new();
                                         private ViewModelAdapter2 _adpater2 = new();

                                         public Driver()
                                         {
                                             _adpater1.Build();
                                         }
                                     }
                                     """;

    [Test]
    public void WithoutInstanceCall_HierarchyRestrictionExcludesSiblingAdapter()
    {
        var result = FollowIncomingCalls("ViewModelAdapter1.AddToSlave");

        Assert.That(RelationshipsOf(result), Is.EquivalentTo(new[]
        {
            "ViewModelAdapter1.AddToSlave -(Overrides)-> Base.AddToSlave",
            "ViewModelAdapter1.AddToSlave -(Calls)-> Base.AddToSlave",
            "Base.Build -(Calls)-> Base.AddToSlave",
            "Driver..ctor -(Calls)-> Base.Build"
        }));

        Assert.That(ElementsOf(result), Is.EquivalentTo(new[]
        {
            "Base.AddToSlave",
            "ViewModelAdapter1.AddToSlave",
            "Base.Build",
            "Driver..ctor"
        }));
    }
}
