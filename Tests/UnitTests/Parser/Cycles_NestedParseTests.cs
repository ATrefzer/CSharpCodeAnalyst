namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Nested-class dependency cycles across two outer classes (OuterClassA/B) and across multiple nesting
///     levels (Level1A/B). The self-referencing and generic nested classes deliberately do NOT form a
///     cycle group, so exactly two are expected. Migrated from the former Core.Cycles approval fixture
///     (NestedClassCycles.cs).
/// </summary>
[TestFixture]
public class Cycles_NestedParseTests : InMemoryCycleParseTestBase
{
    protected override string Code => """
                                     namespace Core.Cycles;

                                     public class OuterClassA
                                     {
                                         public class DirectChildA
                                         {
                                             public OuterClassB.MiddleClassB.NestedInnerB? RelatedB;
                                             public void UseB() { RelatedB?.ProcessB(); }
                                         }

                                         public class MiddleClassA
                                         {
                                             public class NestedInnerA
                                             {
                                                 public OuterClassB.DirectChildB? RelatedDirectB;
                                                 public void UseDirectB() { RelatedDirectB?.ProcessDirectB(); }
                                             }
                                         }
                                     }

                                     public class OuterClassB
                                     {
                                         public class DirectChildB
                                         {
                                             public OuterClassA.DirectChildA? RelatedA;
                                             public void ProcessDirectB() { RelatedA?.UseB(); }
                                         }

                                         public class MiddleClassB
                                         {
                                             public class NestedInnerB
                                             {
                                                 public OuterClassA.DirectChildA? RelatedA;
                                                 public void ProcessB() { RelatedA?.UseB(); }
                                             }
                                         }
                                     }

                                     // Self-referencing nested classes: no multi-node cycle group.
                                     public class SelfReferencingOuter
                                     {
                                         public SelfReferencingInner? RootInner;

                                         public void CreateHierarchy()
                                         {
                                             RootInner = new SelfReferencingInner();
                                             var child = new SelfReferencingInner();
                                             child.LinkToParent(RootInner);
                                         }

                                         public class SelfReferencingInner
                                         {
                                             public SelfReferencingInner? Child;
                                             public SelfReferencingInner? Parent;

                                             public void LinkToParent(SelfReferencingInner parent)
                                             {
                                                 Parent = parent;
                                                 parent.Child = this;
                                             }
                                         }
                                     }

                                     public class Level1A
                                     {
                                         public class Level2A
                                         {
                                             public void ProcessLevel2A()
                                             {
                                                 var level3 = new Level3A();
                                                 level3.UseCrossReference();
                                             }

                                             public class Level3A
                                             {
                                                 public Level1B.Level2B? CrossReference;
                                                 public void UseCrossReference() { CrossReference?.ProcessLevel2B(); }
                                             }
                                         }
                                     }

                                     public class Level1B
                                     {
                                         public class Level2B
                                         {
                                             public Level1A.Level2A.Level3A? BackReference;
                                             public void ProcessLevel2B() { }
                                             public void UseBackReference() { BackReference?.UseCrossReference(); }
                                         }
                                     }

                                     // Generic nested class self-reference: no multi-node cycle group.
                                     public class GenericOuter<T>
                                     {
                                         public class GenericNested<U>
                                         {
                                             public GenericOuter<U>.GenericNested<T>? CrossGeneric;
                                             public void UseCrossGeneric() { CrossGeneric?.ProcessGeneric(); }
                                             public void ProcessGeneric() { }
                                         }
                                     }
                                     """;

    [Test]
    public void TwoNestedClassCycles_AreDetected()
    {
        AssertCycleGroupCount(2);
    }

    [Test]
    public void NestedClassCycle_OuterClassAB_IsDetected()
    {
        AssertContainsCycle(
            new[]
            {
                "OuterClassA",
                "OuterClassA.DirectChildA",
                "OuterClassA.DirectChildA.UseB",
                "OuterClassA.DirectChildA.RelatedB",
                "OuterClassA.MiddleClassA",
                "OuterClassA.MiddleClassA.NestedInnerA",
                "OuterClassA.MiddleClassA.NestedInnerA.UseDirectB",
                "OuterClassA.MiddleClassA.NestedInnerA.RelatedDirectB",
                "OuterClassB",
                "OuterClassB.DirectChildB",
                "OuterClassB.DirectChildB.ProcessDirectB",
                "OuterClassB.DirectChildB.RelatedA",
                "OuterClassB.MiddleClassB",
                "OuterClassB.MiddleClassB.NestedInnerB",
                "OuterClassB.MiddleClassB.NestedInnerB.ProcessB",
                "OuterClassB.MiddleClassB.NestedInnerB.RelatedA"
            },
            new[]
            {
                "OuterClassA.DirectChildA.UseB -> OuterClassB.MiddleClassB.NestedInnerB.ProcessB",
                "OuterClassA.DirectChildA.RelatedB -> OuterClassB.MiddleClassB.NestedInnerB",
                "OuterClassA.MiddleClassA.NestedInnerA.UseDirectB -> OuterClassB.DirectChildB.ProcessDirectB",
                "OuterClassA.MiddleClassA.NestedInnerA.RelatedDirectB -> OuterClassB.DirectChildB",
                "OuterClassB.DirectChildB.ProcessDirectB -> OuterClassA.DirectChildA.UseB",
                "OuterClassB.DirectChildB.RelatedA -> OuterClassA.DirectChildA",
                "OuterClassB.MiddleClassB.NestedInnerB.ProcessB -> OuterClassA.DirectChildA.UseB",
                "OuterClassB.MiddleClassB.NestedInnerB.RelatedA -> OuterClassA.DirectChildA"
            });
    }

    [Test]
    public void MultiLevelNestedCycle_Level1AB_IsDetected()
    {
        AssertContainsCycle(
            new[]
            {
                "Level1A",
                "Level1A.Level2A",
                "Level1A.Level2A.Level3A",
                "Level1A.Level2A.Level3A.UseCrossReference",
                "Level1A.Level2A.Level3A.CrossReference",
                "Level1B",
                "Level1B.Level2B",
                "Level1B.Level2B.ProcessLevel2B",
                "Level1B.Level2B.UseBackReference",
                "Level1B.Level2B.BackReference"
            },
            new[]
            {
                "Level1A.Level2A.Level3A.UseCrossReference -> Level1B.Level2B.ProcessLevel2B",
                "Level1A.Level2A.Level3A.CrossReference -> Level1B.Level2B",
                "Level1B.Level2B.UseBackReference -> Level1A.Level2A.Level3A.UseCrossReference",
                "Level1B.Level2B.BackReference -> Level1A.Level2A.Level3A"
            });
    }
}
