using CodeGraph.Algorithms.Partitioning;
using CodeGraph.Graph;
using CodeParserTests.Helper;

namespace CodeParserTests.UnitTests.Metrics;

[TestFixture]
public class TypeCohesionAnalysisTests
{
    [SetUp]
    public void SetUp()
    {
        _graph = new TestCodeGraph();
    }

    private TestCodeGraph _graph = null!;

    private void Rel(CodeElement source, CodeElement target, RelationshipType type)
    {
        source.Relationships.Add(new Relationship(source.Id, target.Id, type));
    }

    [Test]
    public void Calculate_CohesiveClass_NotListed()
    {
        // Two methods, one calls the other -> a single connected partition.
        var c = _graph.CreateClass("A");
        var m1 = _graph.CreateMethod("A.M1", c);
        var m2 = _graph.CreateMethod("A.M2", c);
        Rel(m1, m2, RelationshipType.Calls);

        Assert.That(TypeCohesionAnalysis.Calculate(_graph), Is.Empty);
    }

    [Test]
    public void Calculate_MethodsSharingAField_AreCohesive()
    {
        // Two methods both accessing the same field are connected through it -> one partition.
        var c = _graph.CreateClass("A");
        var m1 = _graph.CreateMethod("A.M1", c);
        var m2 = _graph.CreateMethod("A.M2", c);
        var field = _graph.CreateField("A.f", c);
        Rel(m1, field, RelationshipType.Uses);
        Rel(m2, field, RelationshipType.Uses);

        Assert.That(TypeCohesionAnalysis.Calculate(_graph), Is.Empty);
    }

    [Test]
    public void Calculate_TwoIndependentGroups_ListedWithTwoPartitions()
    {
        // m1<->m2 and m3<->m4, no cross-links -> two independent partitions.
        var c = _graph.CreateClass("A");
        var m1 = _graph.CreateMethod("A.M1", c);
        var m2 = _graph.CreateMethod("A.M2", c);
        var m3 = _graph.CreateMethod("A.M3", c);
        var m4 = _graph.CreateMethod("A.M4", c);
        Rel(m1, m2, RelationshipType.Calls);
        Rel(m3, m4, RelationshipType.Calls);

        var result = TypeCohesionAnalysis.Calculate(_graph);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Type.Id, Is.EqualTo("A"));
            Assert.That(result[0].PartitionCount, Is.EqualTo(2));
            Assert.That(result[0].MemberCount, Is.EqualTo(4));
            // Two balanced groups of two -> the biggest holds half.
            Assert.That(result[0].LargestPartitionShare, Is.EqualTo(0.5).Within(1e-9));
        });
    }

    [Test]
    public void Calculate_UnbalancedSplit_ReportsHighLargestShare()
    {
        // One connected group of three plus a single isolated method -> 3/4 in the biggest.
        var c = _graph.CreateClass("A");
        var m1 = _graph.CreateMethod("A.M1", c);
        var m2 = _graph.CreateMethod("A.M2", c);
        var m3 = _graph.CreateMethod("A.M3", c);
        _graph.CreateMethod("A.M4", c);
        Rel(m1, m2, RelationshipType.Calls);
        Rel(m2, m3, RelationshipType.Calls);

        var result = TypeCohesionAnalysis.Calculate(_graph);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].PartitionCount, Is.EqualTo(2));
            Assert.That(result[0].LargestPartitionShare, Is.EqualTo(0.75).Within(1e-9));
        });
    }

    [Test]
    public void Calculate_DataClass_Skipped()
    {
        // Only fields, no methods: would split into many partitions, but it is a data holder.
        var c = _graph.CreateClass("Dto");
        _graph.CreateField("Dto.a", c);
        _graph.CreateField("Dto.b", c);
        _graph.CreateField("Dto.c", c);

        Assert.That(TypeCohesionAnalysis.Calculate(_graph), Is.Empty);
    }

    [Test]
    public void Calculate_SingleMethodClass_Skipped()
    {
        // Fewer than two methods -> treated as a data holder, not analyzed.
        var c = _graph.CreateClass("A");
        _graph.CreateMethod("A.M1", c);
        _graph.CreateField("A.f1", c);
        _graph.CreateField("A.f2", c);

        Assert.That(TypeCohesionAnalysis.Calculate(_graph), Is.Empty);
    }

    [Test]
    public void Calculate_ExternalClass_Skipped()
    {
        var external = new CodeElement("Ext", CodeElementType.Class, "Ext", "Ext", null) { IsExternal = true };
        _graph.Nodes["Ext"] = external;
        var m1 = new CodeElement("Ext.M1", CodeElementType.Method, "M1", "Ext.M1", external);
        var m2 = new CodeElement("Ext.M2", CodeElementType.Method, "M2", "Ext.M2", external);
        external.Children.Add(m1);
        external.Children.Add(m2);
        _graph.Nodes["Ext.M1"] = m1;
        _graph.Nodes["Ext.M2"] = m2;
        // No links between m1 and m2 -> would be two partitions if analyzed.

        Assert.That(TypeCohesionAnalysis.Calculate(_graph), Is.Empty);
    }

    [Test]
    public void Calculate_SortsByPartitionCountThenMemberCount()
    {
        // Class B: 3 isolated methods -> 3 partitions. Class A: 2 isolated methods -> 2 partitions.
        var a = _graph.CreateClass("A");
        _graph.CreateMethod("A.M1", a);
        _graph.CreateMethod("A.M2", a);

        var b = _graph.CreateClass("B");
        _graph.CreateMethod("B.M1", b);
        _graph.CreateMethod("B.M2", b);
        _graph.CreateMethod("B.M3", b);

        var result = TypeCohesionAnalysis.Calculate(_graph);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0].Type.Id, Is.EqualTo("B"), "More partitions first");
            Assert.That(result[0].PartitionCount, Is.EqualTo(3));
            Assert.That(result[1].Type.Id, Is.EqualTo("A"));
            Assert.That(result[1].PartitionCount, Is.EqualTo(2));
        });
    }
}
