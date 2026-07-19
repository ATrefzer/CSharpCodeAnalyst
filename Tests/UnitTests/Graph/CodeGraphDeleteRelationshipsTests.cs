using CodeParserTests.Helper;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Graph;

/// <summary>
///     Deleting relationships from the graph. The lookup is set based now, and one pass per affected
///     source node instead of one pass per relationship.
/// </summary>
[TestFixture]
public class CodeGraphDeleteRelationshipsTests
{
    [SetUp]
    public void SetUp()
    {
        _graph = new TestCodeGraph();
        _a = _graph.CreateClass("A");
        _b = _graph.CreateClass("B");
        _c = _graph.CreateClass("C");
    }

    private TestCodeGraph _graph = null!;
    private CodeElement _a = null!;
    private CodeElement _b = null!;
    private CodeElement _c = null!;

    private Relationship Link(CodeElement source, CodeElement target, RelationshipType type)
    {
        var relationship = new Relationship(source.Id, target.Id, type);
        source.Relationships.Add(relationship);
        return relationship;
    }

    [Test]
    public void DeletesOnlyTheGivenRelationships()
    {
        var toDelete = Link(_a, _b, RelationshipType.Calls);
        var toKeep = Link(_a, _c, RelationshipType.Uses);

        var removed = _graph.DeleteRelationships([toDelete]);

        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.True);
            Assert.That(_a.Relationships, Is.EquivalentTo(new[] { toKeep }));
        });
    }

    [Test]
    public void DeletesSeveralRelationshipsOfTheSameSource()
    {
        // The previous implementation ran one removal pass per relationship; both must still go.
        var first = Link(_a, _b, RelationshipType.Calls);
        var second = Link(_a, _c, RelationshipType.Uses);

        var removed = _graph.DeleteRelationships([first, second]);

        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.True);
            Assert.That(_a.Relationships, Is.Empty);
        });
    }

    [Test]
    public void DeletesAcrossSeveralSources()
    {
        var fromA = Link(_a, _c, RelationshipType.Calls);
        var fromB = Link(_b, _c, RelationshipType.Calls);

        var removed = _graph.DeleteRelationships([fromA, fromB]);

        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.True);
            Assert.That(_a.Relationships, Is.Empty);
            Assert.That(_b.Relationships, Is.Empty);
        });
    }

    [Test]
    public void ReturnsFalseWhenNothingWasRemoved()
    {
        Link(_a, _b, RelationshipType.Calls);

        // Same endpoints, different type - not the relationship that exists.
        var notInGraph = new Relationship(_a.Id, _b.Id, RelationshipType.Uses);

        Assert.Multiple(() =>
        {
            Assert.That(_graph.DeleteRelationships([notInGraph]), Is.False);
            Assert.That(_graph.DeleteRelationships([]), Is.False);
            Assert.That(_a.Relationships, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void UnknownSourceElement_IsIgnored()
    {
        // A relationship whose source is no longer in the graph used to throw a KeyNotFoundException.
        var orphan = new Relationship("gone", _b.Id, RelationshipType.Calls);

        Assert.That(() => _graph.DeleteRelationships([orphan]), Throws.Nothing);
    }
}
