namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     FollowIncomingCallsHeuristically: Base.Helper is reached over two paths with different contexts -
///     the abstraction walk (context forbids the sibling Right) and the event-invoker path (context reset
///     by the instance call in OnRaised). Right.X is a real origin only visible under the second context,
///     so the result must be the union over all paths. Migrated from the former CodeExplorerApprovalTests
///     scenario (FollowHeuristic/ContextRace.cs).
/// </summary>
[TestFixture]
public class FollowHeuristic_ContextRaceParseTests : InMemoryFollowIncomingCallsTestBase
{
    protected override string Code => """
                                     using System;

                                     namespace FollowHeuristic.ContextRace;

                                     public abstract class Base
                                     {
                                         public event EventHandler? Raised;

                                         public abstract void Target();

                                         public virtual void Helper()
                                         {
                                             Target();
                                             Raised?.Invoke(this, EventArgs.Empty);
                                         }

                                         public void Register() { Raised += OnRaised; }

                                         public void OnRaised(object? sender, EventArgs e)
                                         {
                                             if (sender is Left left)
                                             {
                                                 left.Target();
                                             }
                                         }
                                     }

                                     public class Left : Base
                                     {
                                         public override void Target() { }
                                     }

                                     public class Right : Base
                                     {
                                         public override void Target() { }

                                         public void X() { Helper(); }
                                     }
                                     """;

    [Test]
    public void ElementIsReprocessedWithLessRestrictiveContext()
    {
        var result = FollowIncomingCalls("Left.Target");

        Assert.That(RelationshipsOf(result), Is.EquivalentTo(new[]
        {
            "Base.OnRaised -(Calls)-> Left.Target",
            "Left.Target -(Overrides)-> Base.Target",
            "Base.Helper -(Calls)-> Base.Target",
            "Base.OnRaised -(Handles)-> Base.Raised",
            "Base.Helper -(Invokes)-> Base.Raised",
            "Right.X -(Calls)-> Base.Helper"
        }));

        Assert.That(ElementsOf(result), Is.EquivalentTo(new[]
        {
            "Left.Target", "Base.OnRaised", "Base.Target", "Base.Helper", "Base.Raised", "Right.X"
        }));
    }
}
