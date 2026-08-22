using CodeParserTests.Helper;
using CSharpCodeAnalyst.CodeGraph.Algorithms.Partitioning;
using CSharpCodeAnalyst.CodeGraph.Graph;

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

        Assert.That(TypeCohesionAnalysis.Calculate(_graph, 2), Is.Empty);
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

        Assert.That(TypeCohesionAnalysis.Calculate(_graph, 2), Is.Empty);
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

        var result = TypeCohesionAnalysis.Calculate(_graph, 2);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Type.Id, Is.EqualTo("A"));
            Assert.That(result[0].PartitionCount, Is.EqualTo(2));
            Assert.That(result[0].MethodCount, Is.EqualTo(4));
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

        var result = TypeCohesionAnalysis.Calculate(_graph, 2);

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

        Assert.That(TypeCohesionAnalysis.Calculate(_graph, 2), Is.Empty);
    }

    [Test]
    public void Calculate_SingleMethodClass_Skipped()
    {
        // Fewer than two methods -> treated as a data holder, not analyzed.
        var c = _graph.CreateClass("A");
        _graph.CreateMethod("A.M1", c);
        _graph.CreateField("A.f1", c);
        _graph.CreateField("A.f2", c);

        Assert.That(TypeCohesionAnalysis.Calculate(_graph, 2), Is.Empty);
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

        Assert.That(TypeCohesionAnalysis.Calculate(_graph, 2), Is.Empty);
    }

    [Test]
    public void Calculate_SortsByPartitionCountThenMethodCount()
    {
        // Class B: 3 isolated methods -> 3 partitions. Class A: 2 isolated methods -> 2 partitions.
        var a = _graph.CreateClass("A");
        _graph.CreateMethod("A.M1", a);
        _graph.CreateMethod("A.M2", a);

        var b = _graph.CreateClass("B");
        _graph.CreateMethod("B.M1", b);
        _graph.CreateMethod("B.M2", b);
        _graph.CreateMethod("B.M3", b);

        var result = TypeCohesionAnalysis.Calculate(_graph, 2);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0].Type.Id, Is.EqualTo("B"), "More partitions first");
            Assert.That(result[0].PartitionCount, Is.EqualTo(3));
            Assert.That(result[1].Type.Id, Is.EqualTo("A"));
            Assert.That(result[1].PartitionCount, Is.EqualTo(2));
        });
    }

    [Test]
    public void Calculate_ConstructorDoesNotHideASplit()
    {
        // The constructor writes both fields, so with it in the graph everything is one partition.
        // It is exactly the hub that makes an incohesive class look cohesive.
        var c = BuildTwoGroupsAroundTwoFields();
        _graph.CreateConstructor("A..ctor", c);
        var ctor = _graph.Nodes["A..ctor"];
        Rel(ctor, _graph.Nodes["A.f1"], RelationshipType.Uses);
        Rel(ctor, _graph.Nodes["A.f2"], RelationshipType.Uses);

        var result = TypeCohesionAnalysis.Calculate(_graph, 2);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(1), "The split must survive the constructor");
            Assert.That(result[0].PartitionCount, Is.EqualTo(2));
            Assert.That(result[0].LargestPartitionShare, Is.EqualTo(0.5).Within(1e-9));
        });
    }

    [Test]
    public void Calculate_StateOnlyTouchedByTheConstructor_IsNotAPartition()
    {
        // Dropping the constructor leaves such a field connected to nothing. It must not count as a
        // way the behavior splits - the view shows it in a group of its own instead.
        var c = BuildTwoGroupsAroundTwoFields();
        var injected = _graph.CreateField("A.injected", c);
        _graph.CreateConstructor("A..ctor", c);
        var ctor = _graph.Nodes["A..ctor"];
        Rel(ctor, injected, RelationshipType.Uses);

        var result = TypeCohesionAnalysis.Calculate(_graph, 2);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].PartitionCount, Is.EqualTo(2), "Two groups of behavior, not three");

            // The share is computed over methods, so the extra field does not skew it.
            Assert.That(result[0].LargestPartitionShare, Is.EqualTo(0.5).Within(1e-9));
        });
    }

    [Test]
    public void Calculate_ConstructorsDoNotCountTowardTheThreshold()
    {
        // Three constructor overloads and two methods is not a class with five members worth
        // measuring - only the two methods can be analyzed at all.
        var c = _graph.CreateClass("A");
        _graph.CreateConstructor("A..ctor1", c);
        _graph.CreateConstructor("A..ctor2", c);
        _graph.CreateConstructor("A..ctor3", c);
        _graph.CreateMethod("A.M1", c);
        _graph.CreateMethod("A.M2", c);

        Assert.That(TypeCohesionAnalysis.Calculate(_graph, 3), Is.Empty);
    }

    [Test]
    public void Calculate_FinalizerAndStaticConstructor_AreLifecycleMembersToo()
    {
        // Same hub argument: both touch state without expressing how the class decomposes.
        var c = BuildTwoGroupsAroundTwoFields();
        var cctor = new CodeElement("A..cctor", CodeElementType.Method, ".cctor", "A..cctor", c);
        var finalizer = new CodeElement("A.Finalize", CodeElementType.Method, "Finalize", "A.Finalize", c);
        foreach (var member in new[] { cctor, finalizer })
        {
            c.Children.Add(member);
            _graph.Nodes[member.Id] = member;
            Rel(member, _graph.Nodes["A.f1"], RelationshipType.Uses);
            Rel(member, _graph.Nodes["A.f2"], RelationshipType.Uses);
        }

        var result = TypeCohesionAnalysis.Calculate(_graph, 2);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].PartitionCount, Is.EqualTo(2));
    }

    [Test]
    public void Split_SeparatesDetachedStateFromTheBehaviorGroups()
    {
        // What the partition view shows: two groups plus everything no method touches, in one
        // bucket rather than one group per member.
        var c = BuildTwoGroupsAroundTwoFields();
        var injected = _graph.CreateField("A.injected", c);
        var exposed = _graph.CreateProperty("A.Exposed", c);
        var ctor = _graph.CreateConstructor("A..ctor", c);
        Rel(ctor, injected, RelationshipType.Uses);
        Rel(ctor, exposed, RelationshipType.Uses);

        var partitions = CodeElementPartitioner.GetPartitions(_graph, c, PartitionOptions.Cohesion);
        var split = TypeCohesionAnalysis.Split(c, partitions);

        Assert.Multiple(() =>
        {
            Assert.That(split.Behavior, Has.Count.EqualTo(2));
            Assert.That(split.DetachedState, Is.EquivalentTo(new[] { injected.Id, exposed.Id }));

            // Nothing gets lost on the way: every member is either in a group or in the bucket.
            var shown = split.Behavior.SelectMany(p => p).Concat(split.DetachedState).ToHashSet();
            Assert.That(shown, Is.EquivalentTo(partitions.SelectMany(p => p)));
        });
    }

    /// <summary>
    ///     Class A: M1/M2 work on f1, M3/M4 work on f2. Two responsibilities, no overlap.
    /// </summary>
    private CodeElement BuildTwoGroupsAroundTwoFields()
    {
        var c = _graph.CreateClass("A");
        var f1 = _graph.CreateField("A.f1", c);
        var f2 = _graph.CreateField("A.f2", c);
        foreach (var name in new[] { "A.M1", "A.M2" })
        {
            Rel(_graph.CreateMethod(name, c), f1, RelationshipType.Uses);
        }

        foreach (var name in new[] { "A.M3", "A.M4" })
        {
            Rel(_graph.CreateMethod(name, c), f2, RelationshipType.Uses);
        }

        return c;
    }
}
