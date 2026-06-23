using CodeGraph.Graph;
using CodeParser.Parser;
using CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Integration test for the SplitPropertyAccessors parser option. Parses the TestSuite solution once
///     with the option enabled and verifies that properties are split into get_/set_ accessor children and
///     that accessor bodies are attributed to the accessor element (not the property container).
/// </summary>
[TestFixture]
public class SplitPropertyAccessorsTests
{
    private CodeGraph.Graph.CodeGraph _graph = null!;

    [OneTimeSetUp]
    public async Task FixtureSetup()
    {
        // The locator may already be registered by another fixture; registering twice is harmless.
        try
        {
            Initializer.InitializeMsBuildLocator();
        }
        catch
        {
            // already registered
        }

        var parser = new CodeParser.Parser.Parser(new ParserConfig(new ProjectExclusionRegExCollection(), false,
            splitPropertyAccessors: true));
        _graph = await parser.ParseAsync(@"..\..\..\..\TestSuite\TestSuite.sln");
    }

    [Test]
    public void PropertyAccessorNodes_AreCreated_AndCorrectlyShaped()
    {
        var accessors = _graph.Nodes.Values
            .Where(n => n.ElementType == CodeElementType.PropertyAccessor)
            .ToList();

        Assert.That(accessors, Is.Not.Empty, "Expected property accessor nodes to be created.");

        Assert.Multiple(() =>
        {
            foreach (var accessor in accessors)
            {
                Assert.That(accessor.Name, Does.StartWith("get_").Or.StartWith("set_"),
                    $"Accessor '{accessor.FullName}' has an unexpected name.");
                Assert.That(accessor.Parent?.ElementType, Is.EqualTo(CodeElementType.Property),
                    $"Accessor '{accessor.FullName}' must be a child of a property.");
            }
        });
    }

    [Test]
    public void GetterOnlyProperty_HasSingleGetterChild()
    {
        // Facade.Value has only a getter.
        var property = FindNode("FollowHeuristic.global.FollowHeuristic.PropertyChain.Facade.Value", CodeElementType.Property);

        var accessorChildren = property.Children
            .Where(c => c.ElementType == CodeElementType.PropertyAccessor)
            .ToList();

        Assert.That(accessorChildren.Select(c => c.Name), Is.EquivalentTo(new[] { "get_Value" }));
    }

    [Test]
    public void AccessorBody_IsAttributedToAccessor_NotToPropertyContainer()
    {
        // Facade.Value { get { return _repository.Compute(); } }
        var property = FindNode("FollowHeuristic.global.FollowHeuristic.PropertyChain.Facade.Value", CodeElementType.Property);
        var getter = property.Children.Single(c => c.Name == "get_Value");
        var compute = FindNode("FollowHeuristic.global.FollowHeuristic.PropertyChain.Repository.Compute", CodeElementType.Method);

        // The "Calls" edge to Repository.Compute must originate from the getter ...
        Assert.That(HasRelationship(getter, compute, RelationshipType.Calls), Is.True,
            "Getter body should call Repository.Compute.");

        // ... and the property container must no longer carry that outgoing call.
        Assert.That(HasRelationship(property, compute, RelationshipType.Calls), Is.False,
            "The property container should not carry the accessor's call any more.");
    }

    private static bool HasRelationship(CodeElement source, CodeElement target, RelationshipType type)
    {
        return source.Relationships.Any(r => r.TargetId == target.Id && r.Type == type);
    }

    private CodeElement FindNode(string fullName, CodeElementType type)
    {
        return _graph.Nodes.Values.Single(n => n.FullName == fullName && n.ElementType == type);
    }
}
