using CodeGraph.Algorithms.Partitioning;
using CodeGraph.Graph;
using CodeParserTests.Helper;

namespace CodeParserTests.UnitTests.Partitioning;

[TestFixture]
public class CodeElementPartitionerBaseClassTests
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

    /// <summary>
    ///     A and B share the inherited field, C is unrelated. Base class B: Base._x.
    /// </summary>
    private (CodeElement derived, CodeElement a, CodeElement b, CodeElement c) BuildDerivedSharingBaseField()
    {
        var baseClass = _graph.CreateClass("Base");
        var x = _graph.CreateField("Base._x", baseClass);

        var derived = _graph.CreateClass("Derived");
        Rel(derived, baseClass, RelationshipType.Inherits);
        var a = _graph.CreateMethod("Derived.A", derived);
        var b = _graph.CreateMethod("Derived.B", derived);
        var c = _graph.CreateMethod("Derived.C", derived);
        Rel(a, x, RelationshipType.Uses);
        Rel(b, x, RelationshipType.Uses);

        return (derived, a, b, c);
    }

    [Test]
    public void WithoutBase_SharedInheritedField_LooksIsolated()
    {
        // Documents the limitation: without base awareness the inherited link is invisible.
        var (derived, _, _, _) = BuildDerivedSharingBaseField();

        var partitions = CodeElementPartitioner.GetPartitions(_graph, derived, false);

        Assert.That(partitions, Has.Count.EqualTo(3));
    }

    [Test]
    public void WithBase_SharedInheritedField_ConnectsThroughBase()
    {
        var (derived, a, b, c) = BuildDerivedSharingBaseField();

        var partitions = CodeElementPartitioner.GetPartitions(_graph, derived, true);

        Assert.Multiple(() =>
        {
            Assert.That(partitions, Has.Count.EqualTo(2));

            // Base member is a connector only - it must not appear in the result.
            var all = partitions.SelectMany(p => p).ToHashSet();
            Assert.That(all, Is.EquivalentTo(new[] { a.Id, b.Id, c.Id }));

            Assert.That(partitions.Any(p => p.SetEquals(new[] { a.Id, b.Id })), "A and B share the inherited field");
            Assert.That(partitions.Any(p => p.SetEquals(new[] { c.Id })), "C stays separate");
        });
    }

    [Test]
    public void WithBase_CallingSameBaseMethod_IsCohesive()
    {
        var baseClass = _graph.CreateClass("Base");
        var m = _graph.CreateMethod("Base.M", baseClass);

        var derived = _graph.CreateClass("Derived");
        Rel(derived, baseClass, RelationshipType.Inherits);
        var a = _graph.CreateMethod("Derived.A", derived);
        var b = _graph.CreateMethod("Derived.B", derived);
        Rel(a, m, RelationshipType.Calls);
        Rel(b, m, RelationshipType.Calls);

        var partitions = CodeElementPartitioner.GetPartitions(_graph, derived, true);

        Assert.That(partitions, Has.Count.EqualTo(1));
        Assert.That(partitions[0], Is.EquivalentTo(new[] { a.Id, b.Id }));
    }

    [Test]
    public void WithBase_OverridesIsNotAConnector()
    {
        // An Overrides edge is structural, not member interaction, so it must not merge members.
        var baseClass = _graph.CreateClass("Base");
        var m = _graph.CreateMethod("Base.M", baseClass);

        var derived = _graph.CreateClass("Derived");
        Rel(derived, baseClass, RelationshipType.Inherits);
        var a = _graph.CreateMethod("Derived.A", derived);
        var b = _graph.CreateMethod("Derived.B", derived);
        Rel(a, m, RelationshipType.Overrides);

        var partitions = CodeElementPartitioner.GetPartitions(_graph, derived, true);

        Assert.That(partitions, Has.Count.EqualTo(2), "Overrides does not connect A to anything");
    }

    [Test]
    public void WithBase_ExternalBase_IsIgnored()
    {
        // Deriving from a framework type: its members are not in the graph, so nothing connects.
        var external = new CodeElement("Ext", CodeElementType.Class, "Ext", "Ext", null) { IsExternal = true };
        _graph.Nodes["Ext"] = external;

        var derived = _graph.CreateClass("Derived");
        Rel(derived, external, RelationshipType.Inherits);
        _graph.CreateMethod("Derived.A", derived);
        _graph.CreateMethod("Derived.B", derived);

        var partitions = CodeElementPartitioner.GetPartitions(_graph, derived, true);

        Assert.That(partitions, Has.Count.EqualTo(2));
    }
}
