namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     The simplest class-level field cycle: two classes referencing each other through a field. Migrated
///     from the former Core.Cycles approval fixture (FieldCycles_simple.cs).
/// </summary>
[TestFixture]
public class Cycles_FieldsSimpleParseTests : InMemoryCycleParseTestBase
{
    protected override string Code => """
                                     namespace Cycles.ClassLevel_Fields;

                                     public class Class1
                                     {
                                         private Class2 _field1;
                                     }

                                     public class Class2
                                     {
                                         private Class1 _field1;
                                     }
                                     """;

    [Test]
    public void FieldCycle_BetweenTwoClasses_IsDetected()
    {
        AssertSingleCycle(
            new[] { "Class1", "Class1._field1", "Class2", "Class2._field1" },
            new[]
            {
                "Class1._field1 -> Class2",
                "Class2._field1 -> Class1"
            });
    }
}
