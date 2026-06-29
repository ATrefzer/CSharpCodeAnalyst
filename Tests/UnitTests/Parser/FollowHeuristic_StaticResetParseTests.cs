namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     FollowIncomingCallsHeuristically: Logger.Log is reached via a static call from the virtual method
///     WorkerA.Work. Walking further up must keep the hierarchy restriction - the implicit Work() inside
///     the sibling WorkerB resolves to WorkerBase.Work but can never dispatch to WorkerA.Work, so
///     WorkerB.Other is excluded. Migrated from the former CodeExplorerApprovalTests scenario
///     (FollowHeuristic/StaticReset.cs).
/// </summary>
[TestFixture]
public class FollowHeuristic_StaticResetParseTests : InMemoryFollowIncomingCallsTestBase
{
    protected override string Code => """
                                     namespace FollowHeuristic.StaticReset;

                                     public static class Logger
                                     {
                                         public static void Log() { }
                                     }

                                     public class WorkerBase
                                     {
                                         public virtual void Work() { }
                                         public void Drive() { Work(); }
                                     }

                                     public class WorkerA : WorkerBase
                                     {
                                         public override void Work() { Logger.Log(); }
                                     }

                                     // Intentionally does not override Work.
                                     public class WorkerB : WorkerBase
                                     {
                                         public void Other() { Work(); }
                                     }
                                     """;

    [Test]
    public void HierarchyRestriction_SurvivesStaticCall()
    {
        var result = FollowIncomingCalls("Logger.Log");

        Assert.That(RelationshipsOf(result), Is.EquivalentTo(new[]
        {
            "WorkerA.Work -(Calls)-> Logger.Log",
            "WorkerA.Work -(Overrides)-> WorkerBase.Work",
            "WorkerBase.Drive -(Calls)-> WorkerBase.Work"
        }));

        Assert.That(ElementsOf(result), Is.EquivalentTo(new[]
        {
            "Logger.Log", "WorkerA.Work", "WorkerBase.Work", "WorkerBase.Drive"
        }));
    }
}
