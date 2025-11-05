using System.Diagnostics;
using CodeGraph.Algorithms.Cycles;

namespace CodeParserTests.ApprovalTests;

[TestFixture]
public class CyclesApprovalTests : ApprovalTestBase
{
    private CodeGraph.Graph.CodeGraph GetTestAssemblyGraph()
    {
        return GetTestGraph("Core.Cycles");
    }

    // All cycles we expect fo find in the project
    private readonly List<(string[], string[] edges)> _groupDefinitions =
    [
        (
            [
                "Core.Cycles.global.Cycles.OuterClass.MiddleClass",
                "Core.Cycles.global.Cycles.OuterClass.MiddleClass.NestedInnerClass.x",
                "Core.Cycles.global.Cycles.OuterClass.DirectChildClass",
                "Core.Cycles.global.Cycles.OuterClass.DirectChildClass.x",
                "Core.Cycles.global.Cycles.OuterClass.MiddleClass.NestedInnerClass"
            ],
            [
                "Core.Cycles.global.Cycles.OuterClass.MiddleClass.NestedInnerClass.x -> Core.Cycles.global.Cycles.OuterClass.DirectChildClass",
                "Core.Cycles.global.Cycles.OuterClass.DirectChildClass.x -> Core.Cycles.global.Cycles.OuterClass.MiddleClass.NestedInnerClass"
            ]
        ),


        (
            [
                "Core.Cycles.global.Cycles.ClassLevel_Fields.Class2",
                "Core.Cycles.global.Cycles.ClassLevel_Fields.Class2._field1",
                "Core.Cycles.global.Cycles.ClassLevel_Fields.Class1",
                "Core.Cycles.global.Cycles.ClassLevel_Fields.Class1._field1"
            ],
            [
                "Core.Cycles.global.Cycles.ClassLevel_Fields.Class2._field1 -> Core.Cycles.global.Cycles.ClassLevel_Fields.Class1",
                "Core.Cycles.global.Cycles.ClassLevel_Fields.Class1._field1 -> Core.Cycles.global.Cycles.ClassLevel_Fields.Class2"
            ]
        ),

        (
            [
                "Core.Cycles.global.Core.Cycles.ClassB",
                "Core.Cycles.global.Core.Cycles.ClassB.UseA",
                "Core.Cycles.global.Core.Cycles.ClassA.MethodA",
                "Core.Cycles.global.Core.Cycles.ClassB._fieldA",
                "Core.Cycles.global.Core.Cycles.ClassA",
                "Core.Cycles.global.Core.Cycles.ClassA.UseB",
                "Core.Cycles.global.Core.Cycles.ClassB.DoSomething",
                "Core.Cycles.global.Core.Cycles.ClassA._fieldB"
            ],
            [
                "Core.Cycles.global.Core.Cycles.ClassB.UseA -> Core.Cycles.global.Core.Cycles.ClassA.MethodA",
                "Core.Cycles.global.Core.Cycles.ClassB._fieldA -> Core.Cycles.global.Core.Cycles.ClassA",
                "Core.Cycles.global.Core.Cycles.ClassA.UseB -> Core.Cycles.global.Core.Cycles.ClassB.DoSomething",
                "Core.Cycles.global.Core.Cycles.ClassA._fieldB -> Core.Cycles.global.Core.Cycles.ClassB"
            ]
        ),

        (
            [
                "Core.Cycles.global.Core.Cycles.NodeZ",
                "Core.Cycles.global.Core.Cycles.NodeZ.ProcessZ",
                "Core.Cycles.global.Core.Cycles.NodeX.ProcessX",
                "Core.Cycles.global.Core.Cycles.NodeZ._nodeX",
                "Core.Cycles.global.Core.Cycles.NodeX",
                "Core.Cycles.global.Core.Cycles.NodeY",
                "Core.Cycles.global.Core.Cycles.NodeY.ProcessY",
                "Core.Cycles.global.Core.Cycles.NodeY._nodeZ",
                "Core.Cycles.global.Core.Cycles.NodeX._nodeY"
            ],
            [
                "Core.Cycles.global.Core.Cycles.NodeZ.ProcessZ -> Core.Cycles.global.Core.Cycles.NodeX.ProcessX",
                "Core.Cycles.global.Core.Cycles.NodeX.ProcessX -> Core.Cycles.global.Core.Cycles.NodeY.ProcessY",
                "Core.Cycles.global.Core.Cycles.NodeZ._nodeX -> Core.Cycles.global.Core.Cycles.NodeX",
                "Core.Cycles.global.Core.Cycles.NodeY.ProcessY -> Core.Cycles.global.Core.Cycles.NodeZ.ProcessZ",
                "Core.Cycles.global.Core.Cycles.NodeY._nodeZ -> Core.Cycles.global.Core.Cycles.NodeZ",
                "Core.Cycles.global.Core.Cycles.NodeX._nodeY -> Core.Cycles.global.Core.Cycles.NodeY"
            ]
        ),

        (
            [
                "Core.Cycles.global.Core.Cycles.ComplexC",
                "Core.Cycles.global.Core.Cycles.ComplexC.UseA",
                "Core.Cycles.global.Core.Cycles.ComplexA.UseB1",
                "Core.Cycles.global.Core.Cycles.ComplexC._fieldA",
                "Core.Cycles.global.Core.Cycles.ComplexA",
                "Core.Cycles.global.Core.Cycles.ComplexB",
                "Core.Cycles.global.Core.Cycles.ComplexB.UseA",
                "Core.Cycles.global.Core.Cycles.ComplexA.UseC",
                "Core.Cycles.global.Core.Cycles.ComplexB._fieldA",
                "Core.Cycles.global.Core.Cycles.ComplexB.UseC",
                "Core.Cycles.global.Core.Cycles.ComplexA._fieldB1",
                "Core.Cycles.global.Core.Cycles.ComplexA._fieldB2",
                "Core.Cycles.global.Core.Cycles.ComplexA._fieldC",
                "Core.Cycles.global.Core.Cycles.ComplexA.UseB2",
                "Core.Cycles.global.Core.Cycles.ComplexB._fieldC",
                "Core.Cycles.global.Core.Cycles.ComplexB.MethodB",
                "Core.Cycles.global.Core.Cycles.ComplexC.MethodC"
            ],
            [
                "Core.Cycles.global.Core.Cycles.ComplexC.UseA -> Core.Cycles.global.Core.Cycles.ComplexA.UseB1",
                "Core.Cycles.global.Core.Cycles.ComplexA.UseB1 -> Core.Cycles.global.Core.Cycles.ComplexB.MethodB",
                "Core.Cycles.global.Core.Cycles.ComplexC._fieldA -> Core.Cycles.global.Core.Cycles.ComplexA",
                "Core.Cycles.global.Core.Cycles.ComplexB.UseA -> Core.Cycles.global.Core.Cycles.ComplexA.UseC",
                "Core.Cycles.global.Core.Cycles.ComplexA.UseC -> Core.Cycles.global.Core.Cycles.ComplexC.MethodC",
                "Core.Cycles.global.Core.Cycles.ComplexB._fieldA -> Core.Cycles.global.Core.Cycles.ComplexA",
                "Core.Cycles.global.Core.Cycles.ComplexB.UseC -> Core.Cycles.global.Core.Cycles.ComplexC.MethodC",
                "Core.Cycles.global.Core.Cycles.ComplexB._fieldC -> Core.Cycles.global.Core.Cycles.ComplexC",
                "Core.Cycles.global.Core.Cycles.ComplexA.UseB2 -> Core.Cycles.global.Core.Cycles.ComplexB.MethodB",
                "Core.Cycles.global.Core.Cycles.ComplexA._fieldB2 -> Core.Cycles.global.Core.Cycles.ComplexB",
                "Core.Cycles.global.Core.Cycles.ComplexA._fieldB1 -> Core.Cycles.global.Core.Cycles.ComplexB",
                "Core.Cycles.global.Core.Cycles.ComplexA._fieldC -> Core.Cycles.global.Core.Cycles.ComplexC"
            ]
        ),

        (
            [
                "Core.Cycles.global.Core.Cycles.PropertyCycleB",
                "Core.Cycles.global.Core.Cycles.PropertyCycleB.ProcessB",
                "Core.Cycles.global.Core.Cycles.PropertyCycleA.ProcessA",
                "Core.Cycles.global.Core.Cycles.PropertyCycleB.RelatedA",
                "Core.Cycles.global.Core.Cycles.PropertyCycleA",
                "Core.Cycles.global.Core.Cycles.PropertyCycleA.RelatedB"
            ],
            [
                "Core.Cycles.global.Core.Cycles.PropertyCycleB.ProcessB -> Core.Cycles.global.Core.Cycles.PropertyCycleA.ProcessA",
                "Core.Cycles.global.Core.Cycles.PropertyCycleA.ProcessA -> Core.Cycles.global.Core.Cycles.PropertyCycleB.ProcessB",
                "Core.Cycles.global.Core.Cycles.PropertyCycleB.RelatedA -> Core.Cycles.global.Core.Cycles.PropertyCycleA",
                "Core.Cycles.global.Core.Cycles.PropertyCycleA.RelatedB -> Core.Cycles.global.Core.Cycles.PropertyCycleB"
            ]
        ),

        (
            [
                "Core.Cycles.global.Core.Cycles.OuterClassB",
                "Core.Cycles.global.Core.Cycles.OuterClassB.MiddleClassB.NestedInnerB.ProcessB",
                "Core.Cycles.global.Core.Cycles.OuterClassA.DirectChildA.UseB",
                "Core.Cycles.global.Core.Cycles.OuterClassB.MiddleClassB.NestedInnerB.RelatedA",
                "Core.Cycles.global.Core.Cycles.OuterClassA.DirectChildA",
                "Core.Cycles.global.Core.Cycles.OuterClassB.DirectChildB.ProcessDirectB",
                "Core.Cycles.global.Core.Cycles.OuterClassB.DirectChildB.RelatedA",
                "Core.Cycles.global.Core.Cycles.OuterClassA",
                "Core.Cycles.global.Core.Cycles.OuterClassA.MiddleClassA.NestedInnerA.UseDirectB",
                "Core.Cycles.global.Core.Cycles.OuterClassA.DirectChildA.RelatedB",
                "Core.Cycles.global.Core.Cycles.OuterClassA.MiddleClassA",
                "Core.Cycles.global.Core.Cycles.OuterClassA.MiddleClassA.NestedInnerA",
                "Core.Cycles.global.Core.Cycles.OuterClassA.MiddleClassA.NestedInnerA.RelatedDirectB",
                "Core.Cycles.global.Core.Cycles.OuterClassB.DirectChildB",
                "Core.Cycles.global.Core.Cycles.OuterClassB.MiddleClassB",
                "Core.Cycles.global.Core.Cycles.OuterClassB.MiddleClassB.NestedInnerB"
            ],
            [
                "Core.Cycles.global.Core.Cycles.OuterClassB.MiddleClassB.NestedInnerB.ProcessB -> Core.Cycles.global.Core.Cycles.OuterClassA.DirectChildA.UseB",
                "Core.Cycles.global.Core.Cycles.OuterClassA.DirectChildA.UseB -> Core.Cycles.global.Core.Cycles.OuterClassB.MiddleClassB.NestedInnerB.ProcessB",
                "Core.Cycles.global.Core.Cycles.OuterClassB.MiddleClassB.NestedInnerB.RelatedA -> Core.Cycles.global.Core.Cycles.OuterClassA.DirectChildA",
                "Core.Cycles.global.Core.Cycles.OuterClassB.DirectChildB.ProcessDirectB -> Core.Cycles.global.Core.Cycles.OuterClassA.DirectChildA.UseB",
                "Core.Cycles.global.Core.Cycles.OuterClassB.DirectChildB.RelatedA -> Core.Cycles.global.Core.Cycles.OuterClassA.DirectChildA",
                "Core.Cycles.global.Core.Cycles.OuterClassA.MiddleClassA.NestedInnerA.UseDirectB -> Core.Cycles.global.Core.Cycles.OuterClassB.DirectChildB.ProcessDirectB",
                "Core.Cycles.global.Core.Cycles.OuterClassA.MiddleClassA.NestedInnerA.RelatedDirectB -> Core.Cycles.global.Core.Cycles.OuterClassB.DirectChildB",
                "Core.Cycles.global.Core.Cycles.OuterClassA.DirectChildA.RelatedB -> Core.Cycles.global.Core.Cycles.OuterClassB.MiddleClassB.NestedInnerB"
            ]
        ),

        (
            [
                "Core.Cycles.global.Core.Cycles.Level1B",
                "Core.Cycles.global.Core.Cycles.Level1B.Level2B.UseBackReference",
                "Core.Cycles.global.Core.Cycles.Level1A.Level2A.Level3A.UseCrossReference",
                "Core.Cycles.global.Core.Cycles.Level1B.Level2B.BackReference",
                "Core.Cycles.global.Core.Cycles.Level1A.Level2A.Level3A",
                "Core.Cycles.global.Core.Cycles.Level1A",
                "Core.Cycles.global.Core.Cycles.Level1B.Level2B.ProcessLevel2B",
                "Core.Cycles.global.Core.Cycles.Level1A.Level2A.Level3A.CrossReference",
                "Core.Cycles.global.Core.Cycles.Level1B.Level2B",
                "Core.Cycles.global.Core.Cycles.Level1A.Level2A"
            ],
            [
                "Core.Cycles.global.Core.Cycles.Level1B.Level2B.UseBackReference -> Core.Cycles.global.Core.Cycles.Level1A.Level2A.Level3A.UseCrossReference",
                "Core.Cycles.global.Core.Cycles.Level1A.Level2A.Level3A.UseCrossReference -> Core.Cycles.global.Core.Cycles.Level1B.Level2B.ProcessLevel2B",
                "Core.Cycles.global.Core.Cycles.Level1B.Level2B.BackReference -> Core.Cycles.global.Core.Cycles.Level1A.Level2A.Level3A",
                "Core.Cycles.global.Core.Cycles.Level1A.Level2A.Level3A.CrossReference -> Core.Cycles.global.Core.Cycles.Level1B.Level2B"
            ]
        )
    ];

    private static bool AreEquivalent(string[] expected, HashSet<string> actual)
    {
        var areEquivalent = expected.Length == actual.Count &&
                            !expected.Except(actual).Any() &&
                            !actual.Except(expected).Any();
        return areEquivalent;
    }

    [Test]
    public void Cycles_ShouldBeDetected()
    {
        var groups = CycleFinder.FindCycleGroups(GetTestAssemblyGraph());

        Assert.That(groups.Count, Is.EqualTo(8));

        // We expect to find all cycles
        foreach (var group in groups)
        {
            var found = _groupDefinitions.Any(g =>
            {
                var actualNodes = GetAllNodes(group.CodeGraph);
                var actualRelationships = GetAllRelationships(group.CodeGraph);
                var expectedNodes = g.Item1;
                var expectedRelationships = g.edges;

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

            Assert.That(found);
        }
    }
}