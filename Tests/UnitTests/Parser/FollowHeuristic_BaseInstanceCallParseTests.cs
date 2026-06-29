namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     FollowIncomingCallsHeuristically: a base class calls a base method on another instance held in a
///     field (<c>_base.AddToSlave()</c>). That instance call resets the context, so Base.AddToSlave is
///     reprocessed without hierarchy restriction and the base call from ViewModelAdapter2 becomes a valid
///     origin too. Contrast with <see cref="FollowHeuristic_NoBaseInstanceCallParseTests" />. Migrated from
///     the former CodeExplorerApprovalTests scenario (Old.CSharpLanguage/Regression_FollowIncomingCalls1).
/// </summary>
[TestFixture]
public class FollowHeuristic_BaseInstanceCallParseTests : InMemoryFollowIncomingCallsTestBase
{
    protected override string Code => """
                                     namespace Demo;

                                     internal abstract class Base
                                     {
                                         private Base _base;

                                         protected virtual void AddToSlave()
                                         {
                                             _base.AddToSlave();
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
    public void InstanceCallOnBaseField_ResetsContext_AndIncludesSiblingAdapter()
    {
        var result = FollowIncomingCalls("ViewModelAdapter1.AddToSlave");

        Assert.That(RelationshipsOf(result), Is.EquivalentTo(new[]
        {
            "ViewModelAdapter1.AddToSlave -(Overrides)-> Base.AddToSlave",
            "ViewModelAdapter1.AddToSlave -(Calls)-> Base.AddToSlave",
            "Base.AddToSlave -(Calls)-> Base.AddToSlave",
            "Base.Build -(Calls)-> Base.AddToSlave",
            "Driver..ctor -(Calls)-> Base.Build",
            "ViewModelAdapter2.AddToSlave -(Calls)-> Base.AddToSlave",
            "ViewModelAdapter2.AddToSlave -(Overrides)-> Base.AddToSlave"
        }));

        Assert.That(ElementsOf(result), Is.EquivalentTo(new[]
        {
            "ViewModelAdapter1.AddToSlave",
            "Base.AddToSlave",
            "Base.Build",
            "Driver..ctor",
            "ViewModelAdapter2.AddToSlave"
        }));
    }
}
