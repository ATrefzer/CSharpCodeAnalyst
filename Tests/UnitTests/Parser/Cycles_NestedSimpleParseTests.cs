namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     The simplest nested-class cycle: two classes nested in an outer class reference each other through
///     fields. The cycle spans the inner classes, not the outer container. Migrated from the former
///     Core.Cycles approval fixture (NestedClassCycle_simples.cs).
/// </summary>
[TestFixture]
public class Cycles_NestedSimpleParseTests : InMemoryCycleParseTestBase
{
    protected override string Code => """
                                     namespace Cycles;

                                     internal class OuterClass
                                     {
                                         private class DirectChildClass
                                         {
                                             private MiddleClass.NestedInnerClass x;
                                         }

                                         private class MiddleClass
                                         {
                                             public class NestedInnerClass
                                             {
                                                 private DirectChildClass x;
                                             }
                                         }
                                     }
                                     """;

    [Test]
    public void NestedClassFieldCycle_IsDetected()
    {
        AssertSingleCycle(
            new[]
            {
                "OuterClass.MiddleClass",
                "OuterClass.DirectChildClass",
                "OuterClass.DirectChildClass.x",
                "OuterClass.MiddleClass.NestedInnerClass",
                "OuterClass.MiddleClass.NestedInnerClass.x"
            },
            new[]
            {
                "OuterClass.DirectChildClass.x -> OuterClass.MiddleClass.NestedInnerClass",
                "OuterClass.MiddleClass.NestedInnerClass.x -> OuterClass.DirectChildClass"
            });
    }
}
