namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Class-level field/property dependency cycles: a two-way field cycle (ClassA/ClassB), a three-way
///     cycle (NodeX/Y/Z), a complex multi-field cycle (ComplexA/B/C) and a property-based cycle
///     (PropertyCycleA/B). Each forms its own cycle group. Migrated from the former Core.Cycles approval
///     fixture (FieldCycles.cs).
/// </summary>
[TestFixture]
public class Cycles_FieldsParseTests : InMemoryCycleParseTestBase
{
    protected override string Code => """
                                     namespace Core.Cycles;

                                     public class ClassA
                                     {
                                         private ClassB? _fieldB;
                                         public void UseB() { _fieldB?.DoSomething(); }
                                         public void MethodA() { }
                                     }

                                     public class ClassB
                                     {
                                         private ClassA? _fieldA;
                                         public void UseA() { _fieldA?.MethodA(); }
                                         public void DoSomething() { }
                                     }

                                     public class NodeX
                                     {
                                         private NodeY? _nodeY;
                                         public void ProcessX() { _nodeY?.ProcessY(); }
                                     }

                                     public class NodeY
                                     {
                                         private NodeZ? _nodeZ;
                                         public void ProcessY() { _nodeZ?.ProcessZ(); }
                                     }

                                     public class NodeZ
                                     {
                                         private NodeX? _nodeX;
                                         public void ProcessZ() { _nodeX?.ProcessX(); }
                                     }

                                     public class ComplexA
                                     {
                                         private ComplexB? _fieldB1;
                                         private ComplexB? _fieldB2;
                                         private ComplexC? _fieldC;
                                         public void UseB1() { _fieldB1?.MethodB(); }
                                         public void UseB2() { _fieldB2?.MethodB(); }
                                         public void UseC() { _fieldC?.MethodC(); }
                                     }

                                     public class ComplexB
                                     {
                                         private ComplexA? _fieldA;
                                         private ComplexC? _fieldC;
                                         public void UseA() { _fieldA?.UseC(); }
                                         public void UseC() { _fieldC?.MethodC(); }
                                         public void MethodB() { }
                                     }

                                     public class ComplexC
                                     {
                                         private ComplexA? _fieldA;
                                         public void UseA() { _fieldA?.UseB1(); }
                                         public void MethodC() { }
                                     }

                                     public class PropertyCycleA
                                     {
                                         public PropertyCycleB? RelatedB { get; set; }
                                         public void ProcessA() { RelatedB?.ProcessB(); }
                                     }

                                     public class PropertyCycleB
                                     {
                                         public PropertyCycleA? RelatedA { get; set; }
                                         public void ProcessB() { RelatedA?.ProcessA(); }
                                     }
                                     """;

    [Test]
    public void FourFieldCycles_AreDetected()
    {
        AssertCycleGroupCount(4);
    }

    [Test]
    public void TwoWayFieldCycle_ClassAClassB_IsDetected()
    {
        AssertContainsCycle(
            new[]
            {
                "ClassA", "ClassA.UseB", "ClassA.MethodA", "ClassA._fieldB",
                "ClassB", "ClassB.UseA", "ClassB.DoSomething", "ClassB._fieldA"
            },
            new[]
            {
                "ClassA.UseB -> ClassB.DoSomething",
                "ClassA._fieldB -> ClassB",
                "ClassB.UseA -> ClassA.MethodA",
                "ClassB._fieldA -> ClassA"
            });
    }

    [Test]
    public void ThreeWayCycle_NodeXYZ_IsDetected()
    {
        AssertContainsCycle(
            new[]
            {
                "NodeX", "NodeX.ProcessX", "NodeX._nodeY",
                "NodeY", "NodeY.ProcessY", "NodeY._nodeZ",
                "NodeZ", "NodeZ.ProcessZ", "NodeZ._nodeX"
            },
            new[]
            {
                "NodeX.ProcessX -> NodeY.ProcessY",
                "NodeX._nodeY -> NodeY",
                "NodeY.ProcessY -> NodeZ.ProcessZ",
                "NodeY._nodeZ -> NodeZ",
                "NodeZ.ProcessZ -> NodeX.ProcessX",
                "NodeZ._nodeX -> NodeX"
            });
    }

    [Test]
    public void ComplexMultiFieldCycle_ComplexABC_IsDetected()
    {
        AssertContainsCycle(
            new[]
            {
                "ComplexA", "ComplexA.UseB1", "ComplexA.UseB2", "ComplexA.UseC",
                "ComplexA._fieldB1", "ComplexA._fieldB2", "ComplexA._fieldC",
                "ComplexB", "ComplexB.UseA", "ComplexB.UseC", "ComplexB.MethodB",
                "ComplexB._fieldA", "ComplexB._fieldC",
                "ComplexC", "ComplexC.UseA", "ComplexC.MethodC", "ComplexC._fieldA"
            },
            new[]
            {
                "ComplexA.UseB1 -> ComplexB.MethodB",
                "ComplexA.UseB2 -> ComplexB.MethodB",
                "ComplexA.UseC -> ComplexC.MethodC",
                "ComplexA._fieldB1 -> ComplexB",
                "ComplexA._fieldB2 -> ComplexB",
                "ComplexA._fieldC -> ComplexC",
                "ComplexB.UseA -> ComplexA.UseC",
                "ComplexB.UseC -> ComplexC.MethodC",
                "ComplexB._fieldA -> ComplexA",
                "ComplexB._fieldC -> ComplexC",
                "ComplexC.UseA -> ComplexA.UseB1",
                "ComplexC._fieldA -> ComplexA"
            });
    }

    [Test]
    public void PropertyCycle_PropertyCycleAB_IsDetected()
    {
        AssertContainsCycle(
            new[]
            {
                "PropertyCycleA", "PropertyCycleA.ProcessA", "PropertyCycleA.RelatedB",
                "PropertyCycleB", "PropertyCycleB.ProcessB", "PropertyCycleB.RelatedA"
            },
            new[]
            {
                "PropertyCycleA.ProcessA -> PropertyCycleB.ProcessB",
                "PropertyCycleA.RelatedB -> PropertyCycleB",
                "PropertyCycleB.ProcessB -> PropertyCycleA.ProcessA",
                "PropertyCycleB.RelatedA -> PropertyCycleA"
            });
    }
}
