using System.Diagnostics;
using CodeParser.Analysis.Cycles;
using Contracts.Graph;

namespace CodeParserTests.ApprovalTests;

[TestFixture]
public class CyclesApprovalTests : ProjectTestBase
{
    private CodeGraph GetTestGraph()
    {
        return GetGraph("Core.Cycles");
    }

    // All cycles we expect fo find in the project
    private readonly List<(string[], string[] edges)> _groupDefinitions =
    [
        (
            [
                "Core.Cycles.Cycles.OuterClass.MiddleClass",
                "Core.Cycles.Cycles.OuterClass.MiddleClass.NestedInnerClass.x",
                "Core.Cycles.Cycles.OuterClass.DirectChildClass",
                "Core.Cycles.Cycles.OuterClass.DirectChildClass.x",
                "Core.Cycles.Cycles.OuterClass.MiddleClass.NestedInnerClass"
            ],
            [
                "Core.Cycles.Cycles.OuterClass.MiddleClass.NestedInnerClass.x -> Core.Cycles.Cycles.OuterClass.DirectChildClass",
                "Core.Cycles.Cycles.OuterClass.DirectChildClass.x -> Core.Cycles.Cycles.OuterClass.MiddleClass.NestedInnerClass"
            ]
        ),


        (
            [
                "Core.Cycles.Cycles.ClassLevel_Fields.Class2",
                "Core.Cycles.Cycles.ClassLevel_Fields.Class2._field1",
                "Core.Cycles.Cycles.ClassLevel_Fields.Class1",
                "Core.Cycles.Cycles.ClassLevel_Fields.Class1._field1"
            ],
            [
                "Core.Cycles.Cycles.ClassLevel_Fields.Class2._field1 -> Core.Cycles.Cycles.ClassLevel_Fields.Class1",
                "Core.Cycles.Cycles.ClassLevel_Fields.Class1._field1 -> Core.Cycles.Cycles.ClassLevel_Fields.Class2"
            ]
        ),

        (
            [
                "Core.Cycles.Core.Cycles.ClassB",
                "Core.Cycles.Core.Cycles.ClassB.UseA",
                "Core.Cycles.Core.Cycles.ClassA.MethodA",
                "Core.Cycles.Core.Cycles.ClassB._fieldA",
                "Core.Cycles.Core.Cycles.ClassA",
                "Core.Cycles.Core.Cycles.ClassA.UseB",
                "Core.Cycles.Core.Cycles.ClassB.DoSomething",
                "Core.Cycles.Core.Cycles.ClassA._fieldB"
            ],
            [
                "Core.Cycles.Core.Cycles.ClassB.UseA -> Core.Cycles.Core.Cycles.ClassA.MethodA",
                "Core.Cycles.Core.Cycles.ClassB._fieldA -> Core.Cycles.Core.Cycles.ClassA",
                "Core.Cycles.Core.Cycles.ClassA.UseB -> Core.Cycles.Core.Cycles.ClassB.DoSomething",
                "Core.Cycles.Core.Cycles.ClassA._fieldB -> Core.Cycles.Core.Cycles.ClassB"
            ]
        ),

        (
            [
                "Core.Cycles.Core.Cycles.NodeZ",
                "Core.Cycles.Core.Cycles.NodeZ.ProcessZ",
                "Core.Cycles.Core.Cycles.NodeX.ProcessX",
                "Core.Cycles.Core.Cycles.NodeZ._nodeX",
                "Core.Cycles.Core.Cycles.NodeX",
                "Core.Cycles.Core.Cycles.NodeY",
                "Core.Cycles.Core.Cycles.NodeY.ProcessY",
                "Core.Cycles.Core.Cycles.NodeY._nodeZ",
                "Core.Cycles.Core.Cycles.NodeX._nodeY"
            ],
            [
                "Core.Cycles.Core.Cycles.NodeZ.ProcessZ -> Core.Cycles.Core.Cycles.NodeX.ProcessX",
                "Core.Cycles.Core.Cycles.NodeX.ProcessX -> Core.Cycles.Core.Cycles.NodeY.ProcessY",
                "Core.Cycles.Core.Cycles.NodeZ._nodeX -> Core.Cycles.Core.Cycles.NodeX",
                "Core.Cycles.Core.Cycles.NodeY.ProcessY -> Core.Cycles.Core.Cycles.NodeZ.ProcessZ",
                "Core.Cycles.Core.Cycles.NodeY._nodeZ -> Core.Cycles.Core.Cycles.NodeZ",
                "Core.Cycles.Core.Cycles.NodeX._nodeY -> Core.Cycles.Core.Cycles.NodeY"
            ]
        ),

        (
            [
                "Core.Cycles.Core.Cycles.ComplexC",
                "Core.Cycles.Core.Cycles.ComplexC.UseA",
                "Core.Cycles.Core.Cycles.ComplexA.UseB1",
                "Core.Cycles.Core.Cycles.ComplexC._fieldA",
                "Core.Cycles.Core.Cycles.ComplexA",
                "Core.Cycles.Core.Cycles.ComplexB",
                "Core.Cycles.Core.Cycles.ComplexB.UseA",
                "Core.Cycles.Core.Cycles.ComplexA.UseC",
                "Core.Cycles.Core.Cycles.ComplexB._fieldA",
                "Core.Cycles.Core.Cycles.ComplexB.UseC",
                "Core.Cycles.Core.Cycles.ComplexA._fieldB1",
                "Core.Cycles.Core.Cycles.ComplexA._fieldB2",
                "Core.Cycles.Core.Cycles.ComplexA._fieldC",
                "Core.Cycles.Core.Cycles.ComplexA.UseB2",
                "Core.Cycles.Core.Cycles.ComplexB._fieldC",
                "Core.Cycles.Core.Cycles.ComplexB.MethodB",
                "Core.Cycles.Core.Cycles.ComplexC.MethodC"
            ],
            [
                "Core.Cycles.Core.Cycles.ComplexC.UseA -> Core.Cycles.Core.Cycles.ComplexA.UseB1",
                "Core.Cycles.Core.Cycles.ComplexA.UseB1 -> Core.Cycles.Core.Cycles.ComplexB.MethodB",
                "Core.Cycles.Core.Cycles.ComplexC._fieldA -> Core.Cycles.Core.Cycles.ComplexA",
                "Core.Cycles.Core.Cycles.ComplexB.UseA -> Core.Cycles.Core.Cycles.ComplexA.UseC",
                "Core.Cycles.Core.Cycles.ComplexA.UseC -> Core.Cycles.Core.Cycles.ComplexC.MethodC",
                "Core.Cycles.Core.Cycles.ComplexB._fieldA -> Core.Cycles.Core.Cycles.ComplexA",
                "Core.Cycles.Core.Cycles.ComplexB.UseC -> Core.Cycles.Core.Cycles.ComplexC.MethodC",
                "Core.Cycles.Core.Cycles.ComplexB._fieldC -> Core.Cycles.Core.Cycles.ComplexC",
                "Core.Cycles.Core.Cycles.ComplexA.UseB2 -> Core.Cycles.Core.Cycles.ComplexB.MethodB",
                "Core.Cycles.Core.Cycles.ComplexA._fieldB2 -> Core.Cycles.Core.Cycles.ComplexB",
                "Core.Cycles.Core.Cycles.ComplexA._fieldB1 -> Core.Cycles.Core.Cycles.ComplexB",
                "Core.Cycles.Core.Cycles.ComplexA._fieldC -> Core.Cycles.Core.Cycles.ComplexC"
            ]
        ),

        (
            [
                "Core.Cycles.Core.Cycles.PropertyCycleB",
                "Core.Cycles.Core.Cycles.PropertyCycleB.ProcessB",
                "Core.Cycles.Core.Cycles.PropertyCycleA.ProcessA",
                "Core.Cycles.Core.Cycles.PropertyCycleB.RelatedA",
                "Core.Cycles.Core.Cycles.PropertyCycleA",
                "Core.Cycles.Core.Cycles.PropertyCycleA.RelatedB"
            ],
            [
                "Core.Cycles.Core.Cycles.PropertyCycleB.ProcessB -> Core.Cycles.Core.Cycles.PropertyCycleA.ProcessA",
                "Core.Cycles.Core.Cycles.PropertyCycleA.ProcessA -> Core.Cycles.Core.Cycles.PropertyCycleB.ProcessB",
                "Core.Cycles.Core.Cycles.PropertyCycleB.RelatedA -> Core.Cycles.Core.Cycles.PropertyCycleA",
                "Core.Cycles.Core.Cycles.PropertyCycleA.RelatedB -> Core.Cycles.Core.Cycles.PropertyCycleB"
            ]
        ),

        (
            [
                "Core.Cycles.Core.Cycles.OuterClassB",
                "Core.Cycles.Core.Cycles.OuterClassB.MiddleClassB.NestedInnerB.ProcessB",
                "Core.Cycles.Core.Cycles.OuterClassA.DirectChildA.UseB",
                "Core.Cycles.Core.Cycles.OuterClassB.MiddleClassB.NestedInnerB.RelatedA",
                "Core.Cycles.Core.Cycles.OuterClassA.DirectChildA",
                "Core.Cycles.Core.Cycles.OuterClassB.DirectChildB.ProcessDirectB",
                "Core.Cycles.Core.Cycles.OuterClassB.DirectChildB.RelatedA",
                "Core.Cycles.Core.Cycles.OuterClassA",
                "Core.Cycles.Core.Cycles.OuterClassA.MiddleClassA.NestedInnerA.UseDirectB",
                "Core.Cycles.Core.Cycles.OuterClassA.DirectChildA.RelatedB",
                "Core.Cycles.Core.Cycles.OuterClassA.MiddleClassA",
                "Core.Cycles.Core.Cycles.OuterClassA.MiddleClassA.NestedInnerA",
                "Core.Cycles.Core.Cycles.OuterClassA.MiddleClassA.NestedInnerA.RelatedDirectB",
                "Core.Cycles.Core.Cycles.OuterClassB.DirectChildB",
                "Core.Cycles.Core.Cycles.OuterClassB.MiddleClassB",
                "Core.Cycles.Core.Cycles.OuterClassB.MiddleClassB.NestedInnerB"
            ],
            [
                "Core.Cycles.Core.Cycles.OuterClassB.MiddleClassB.NestedInnerB.ProcessB -> Core.Cycles.Core.Cycles.OuterClassA.DirectChildA.UseB",
                "Core.Cycles.Core.Cycles.OuterClassA.DirectChildA.UseB -> Core.Cycles.Core.Cycles.OuterClassB.MiddleClassB.NestedInnerB.ProcessB",
                "Core.Cycles.Core.Cycles.OuterClassB.MiddleClassB.NestedInnerB.RelatedA -> Core.Cycles.Core.Cycles.OuterClassA.DirectChildA",
                "Core.Cycles.Core.Cycles.OuterClassB.DirectChildB.ProcessDirectB -> Core.Cycles.Core.Cycles.OuterClassA.DirectChildA.UseB",
                "Core.Cycles.Core.Cycles.OuterClassB.DirectChildB.RelatedA -> Core.Cycles.Core.Cycles.OuterClassA.DirectChildA",
                "Core.Cycles.Core.Cycles.OuterClassA.MiddleClassA.NestedInnerA.UseDirectB -> Core.Cycles.Core.Cycles.OuterClassB.DirectChildB.ProcessDirectB",
                "Core.Cycles.Core.Cycles.OuterClassA.MiddleClassA.NestedInnerA.RelatedDirectB -> Core.Cycles.Core.Cycles.OuterClassB.DirectChildB",
                "Core.Cycles.Core.Cycles.OuterClassA.DirectChildA.RelatedB -> Core.Cycles.Core.Cycles.OuterClassB.MiddleClassB.NestedInnerB"
            ]
        ),

        (
            [
                "Core.Cycles.Core.Cycles.Level1B",
                "Core.Cycles.Core.Cycles.Level1B.Level2B.UseBackReference",
                "Core.Cycles.Core.Cycles.Level1A.Level2A.Level3A.UseCrossReference",
                "Core.Cycles.Core.Cycles.Level1B.Level2B.BackReference",
                "Core.Cycles.Core.Cycles.Level1A.Level2A.Level3A",
                "Core.Cycles.Core.Cycles.Level1A",
                "Core.Cycles.Core.Cycles.Level1B.Level2B.ProcessLevel2B",
                "Core.Cycles.Core.Cycles.Level1A.Level2A.Level3A.CrossReference",
                "Core.Cycles.Core.Cycles.Level1B.Level2B",
                "Core.Cycles.Core.Cycles.Level1A.Level2A"
            ],
            [
                "Core.Cycles.Core.Cycles.Level1B.Level2B.UseBackReference -> Core.Cycles.Core.Cycles.Level1A.Level2A.Level3A.UseCrossReference",
                "Core.Cycles.Core.Cycles.Level1A.Level2A.Level3A.UseCrossReference -> Core.Cycles.Core.Cycles.Level1B.Level2B.ProcessLevel2B",
                "Core.Cycles.Core.Cycles.Level1B.Level2B.BackReference -> Core.Cycles.Core.Cycles.Level1A.Level2A.Level3A",
                "Core.Cycles.Core.Cycles.Level1A.Level2A.Level3A.CrossReference -> Core.Cycles.Core.Cycles.Level1B.Level2B"
            ]
        )
    ];

    private bool AreEquivalent(string[] expected, HashSet<string> actual)
    {
        var areEquivalent = expected.Length == actual.Count &&
                            !expected.Except(actual).Any() &&
                            !actual.Except(expected).Any();
        return areEquivalent;
    }

    [Test]
    public void Cycles_ShouldBeDetected()
    {
        var groups = CycleFinder.FindCycleGroups(GetTestGraph());

        Assert.AreEqual(8, groups.Count);

        // We expect to find all cycles
        foreach (var group in groups)
        {
            var found = _groupDefinitions.Any(g =>
            {
                var actualNodes = GetAllNodes(group.CodeGraph);
                var actualRelationships = GetAllRelationships(group.CodeGraph);
                var expectedNodes = g.Item1;
                var expectedRelationships = g.Item2;

                return AreEquivalent(expectedNodes, actualNodes) &&
                       AreEquivalent(expectedRelationships, actualRelationships);
            });

            if (!found)
            {
                // Dump debug info
                var actualNodes = GetAllNodes(group.CodeGraph);
                var actualRelationships = GetAllRelationships(group.CodeGraph);

                var formattedNodes = DumpCodeElements(actualNodes);
                var formattedRelationships = DumpRelationships(actualRelationships);

                Trace.WriteLine(formattedNodes);
                Trace.WriteLine(formattedRelationships);
            }

            Assert.IsTrue(found);
        }
    }
}