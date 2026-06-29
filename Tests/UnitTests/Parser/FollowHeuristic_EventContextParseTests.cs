namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     FollowIncomingCallsHeuristically: Subscriber and Publisher share a base class, so the start context
///     forbids Publisher. But raising an event dispatches via delegate; the chain Trigger -> Raise ->
///     Changed -> OnChanged is a real origin and must not be filtered by the subscriber's hierarchy
///     restriction. Migrated from the former CodeExplorerApprovalTests scenario
///     (FollowHeuristic/EventContext.cs).
/// </summary>
[TestFixture]
public class FollowHeuristic_EventContextParseTests : InMemoryFollowIncomingCallsTestBase
{
    protected override string Code => """
                                     using System;

                                     namespace FollowHeuristic.EventContext;

                                     public class Base
                                     {
                                     }

                                     public class Subscriber : Base
                                     {
                                         public void Attach(Publisher publisher)
                                         {
                                             publisher.Changed += OnChanged;
                                         }

                                         public void OnChanged(object? sender, EventArgs e) { }
                                     }

                                     public class Publisher : Base
                                     {
                                         public event EventHandler? Changed;

                                         public void Raise() { Changed?.Invoke(this, EventArgs.Empty); }

                                         public void Trigger() { Raise(); }
                                     }
                                     """;

    [Test]
    public void PublisherSide_IsNotFilteredBySubscriberHierarchy()
    {
        var result = FollowIncomingCalls("Subscriber.OnChanged");

        Assert.That(RelationshipsOf(result), Is.EquivalentTo(new[]
        {
            "Subscriber.OnChanged -(Handles)-> Publisher.Changed",
            "Publisher.Raise -(Invokes)-> Publisher.Changed",
            "Publisher.Trigger -(Calls)-> Publisher.Raise"
        }));

        Assert.That(ElementsOf(result), Is.EquivalentTo(new[]
        {
            "Subscriber.OnChanged", "Publisher.Changed", "Publisher.Raise", "Publisher.Trigger"
        }));
    }
}
