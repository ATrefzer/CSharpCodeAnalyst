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
    public void ReadAndWriteOfSameProperty_AreRoutedToDistinctAccessors()
    {
        const string prefix = "ModuleLevel1.global.ModuleLevel1.Model.ModelA";
        var property = FindNode($"{prefix}.ModelCPropertyOfModelA", CodeElementType.Property);
        var getter = property.Children.Single(c => c.Name == "get_ModelCPropertyOfModelA");
        var setter = property.Children.Single(c => c.Name == "set_ModelCPropertyOfModelA");

        // AccessToPropertiesSetter: "ModelCPropertyOfModelA = new ModelC();" -> setter only.
        var writer = FindNode($"{prefix}.AccessToPropertiesSetter", CodeElementType.Method);
        // AccessToPropertiesGetter: "var modelC = ModelCPropertyOfModelA;" -> getter only.
        var reader = FindNode($"{prefix}.AccessToPropertiesGetter", CodeElementType.Method);

        Assert.Multiple(() =>
        {
            Assert.That(HasRelationship(writer, setter, RelationshipType.Calls), Is.True, "Write should target the setter.");
            Assert.That(HasRelationship(writer, getter, RelationshipType.Calls), Is.False, "Write must not target the getter.");

            Assert.That(HasRelationship(reader, getter, RelationshipType.Calls), Is.True, "Read should target the getter.");
            Assert.That(HasRelationship(reader, setter, RelationshipType.Calls), Is.False, "Read must not target the setter.");

            Assert.That(HasRelationship(writer, property, RelationshipType.Calls), Is.False, "Access must not target the property container.");
            Assert.That(HasRelationship(reader, property, RelationshipType.Calls), Is.False, "Access must not target the property container.");
        });
    }

    [Test]
    public void InterfaceImplementation_IsModeledAtAccessorLevel()
    {
        // ServiceBase.IfProperty { get; set; } implements IServiceC.IfProperty { get; set; }
        const string baseProp = "ModuleLevel1.global.ModuleLevel1.ServiceBase.IfProperty";
        const string ifaceProp = "ModuleLevel1.global.ModuleLevel1.IServiceC.IfProperty";

        Assert.Multiple(() =>
        {
            Assert.That(HasRelationship(Accessor(baseProp, "get_IfProperty"), Accessor(ifaceProp, "get_IfProperty"),
                RelationshipType.Implements), Is.True, "Getter should implement the interface getter.");
            Assert.That(HasRelationship(Accessor(baseProp, "set_IfProperty"), Accessor(ifaceProp, "set_IfProperty"),
                RelationshipType.Implements), Is.True, "Setter should implement the interface setter.");

            // The property containers must not carry the implements edge any more.
            var baseContainer = FindNode(baseProp, CodeElementType.Property);
            var ifaceContainer = FindNode(ifaceProp, CodeElementType.Property);
            Assert.That(HasRelationship(baseContainer, ifaceContainer, RelationshipType.Implements), Is.False,
                "The property container should not carry the implements edge when accessors are split.");
        });
    }

    [Test]
    public void PropertyOverride_IsModeledAtAccessorLevel()
    {
        // ServiceC.IfProperty overrides ServiceBase.IfProperty (both get; set;).
        const string derivedProp = "ModuleLevel1.global.ModuleLevel1.ServiceC.IfProperty";
        const string baseProp = "ModuleLevel1.global.ModuleLevel1.ServiceBase.IfProperty";

        Assert.Multiple(() =>
        {
            Assert.That(HasRelationship(Accessor(derivedProp, "get_IfProperty"), Accessor(baseProp, "get_IfProperty"),
                RelationshipType.Overrides), Is.True, "Getter should override the base getter.");
            Assert.That(HasRelationship(Accessor(derivedProp, "set_IfProperty"), Accessor(baseProp, "set_IfProperty"),
                RelationshipType.Overrides), Is.True, "Setter should override the base setter.");

            var derivedContainer = FindNode(derivedProp, CodeElementType.Property);
            var baseContainer = FindNode(baseProp, CodeElementType.Property);
            Assert.That(HasRelationship(derivedContainer, baseContainer, RelationshipType.Overrides), Is.False,
                "The property container should not carry the override edge when accessors are split.");
        });
    }

    private CodeElement Accessor(string propertyFullName, string accessorName)
    {
        var property = FindNode(propertyFullName, CodeElementType.Property);
        return property.Children.Single(c => c.Name == accessorName);
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
