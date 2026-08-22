using CodeParserTests.Helper;
using CSharpCodeAnalyst.CodeGraph.Algorithms.Partitioning;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Partitioning;

/// <summary>
///     Which relationship types put two members of the same class into one partition.
/// </summary>
[TestFixture]
public class CodeElementPartitionerRelationshipTests
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
    public void Handles_DoesNotConnectAHandlerToTheEvent()
    {
        // Handles is derived, not written: the handler's code never mentions the event. Whoever
        // wrote the "+=" knows both - and that may be a different class altogether, which must not
        // decide how this class decomposes.
        var c = _graph.CreateClass("A");
        var changed = _graph.CreateEvent("A.Changed", c);
        var handler = _graph.CreateMethod("A.OnChanged", c);
        var raiser = _graph.CreateMethod("A.Raise", c);
        Rel(handler, changed, RelationshipType.Handles);
        Rel(raiser, changed, RelationshipType.Invokes);

        var partitions = CodeElementPartitioner.GetPartitions(_graph, c, PartitionOptions.Cohesion);

        Assert.Multiple(() =>
        {
            Assert.That(partitions, Has.Count.EqualTo(2));
            Assert.That(partitions.Any(p => p.SetEquals(new[] { changed.Id, raiser.Id })),
                "Invokes is written in the raiser and connects");
            Assert.That(partitions.Any(p => p.SetEquals(new[] { handler.Id })),
                "Nothing inside the class refers to the handler");
        });
    }

    [Test]
    public void TheRegisteringMember_ConnectsTheHandlerAndTheEvent()
    {
        // Where the link really lives. A "+=" produces two Uses edges from the registering member,
        // one to the event and one to the handler method group, so the group forms around it.
        var c = _graph.CreateClass("A");
        var changed = _graph.CreateEvent("A.Changed", c);
        var handler = _graph.CreateMethod("A.OnChanged", c);
        var init = _graph.CreateMethod("A.Init", c);
        Rel(handler, changed, RelationshipType.Handles);
        Rel(init, changed, RelationshipType.Uses);
        Rel(init, handler, RelationshipType.Uses);

        var partitions = CodeElementPartitioner.GetPartitions(_graph, c, PartitionOptions.Cohesion);

        Assert.That(partitions, Has.Count.EqualTo(1));
        Assert.That(partitions[0], Is.EquivalentTo(new[] { changed.Id, handler.Id, init.Id }));
    }

    [Test]
    public void OwnMembers_AreConnectedByAnyReferencingRelationship()
    {
        // Between two members of the same class every relationship that is a real reference counts
        // as interaction, not only Calls and Uses. The restriction to Calls / Uses applies to base
        // class members only, see the base class tests.
        var c = _graph.CreateClass("A");
        var attribute = _graph.CreateClass("A.Marker", c);
        var method = _graph.CreateMethod("A.M", c);
        Rel(method, attribute, RelationshipType.UsesAttribute);

        var partitions = CodeElementPartitioner.GetPartitions(_graph, c, PartitionOptions.Cohesion);

        Assert.That(partitions, Has.Count.EqualTo(1));
    }
}
