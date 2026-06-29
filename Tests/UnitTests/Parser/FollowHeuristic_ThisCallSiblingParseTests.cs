namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     FollowIncomingCallsHeuristically: "this.Process()" inside the sibling class Right resolves to
///     Base.Process but can never dispatch to Left.Process, so it must be filtered like an implicit call;
///     only the instance call from Consumer.Use is a real origin. Migrated from the former
///     CodeExplorerApprovalTests scenario (FollowHeuristic/ThisCallSibling.cs).
/// </summary>
[TestFixture]
public class FollowHeuristic_ThisCallSiblingParseTests : InMemoryFollowIncomingCallsTestBase
{
    protected override string Code => """
                                     namespace FollowHeuristic.ThisCallSibling;

                                     public class Base
                                     {
                                         public virtual void Process() { }
                                     }

                                     public class Left : Base
                                     {
                                         public override void Process() { }
                                     }

                                     // Intentionally does not override Process.
                                     public class Right : Base
                                     {
                                         public void RunWithThis() { this.Process(); }
                                         public void RunImplicit() { Process(); }
                                     }

                                     public class Consumer
                                     {
                                         public void Use(Base b) { b.Process(); }
                                     }
                                     """;

    [Test]
    public void ThisCallFromSibling_IsExcluded()
    {
        var result = FollowIncomingCalls("Left.Process");

        Assert.That(RelationshipsOf(result), Is.EquivalentTo(new[]
        {
            "Left.Process -(Overrides)-> Base.Process",
            "Consumer.Use -(Calls)-> Base.Process"
        }));

        Assert.That(ElementsOf(result), Is.EquivalentTo(new[]
        {
            "Left.Process", "Base.Process", "Consumer.Use"
        }));
    }
}
