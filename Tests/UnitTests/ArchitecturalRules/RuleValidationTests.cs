using CodeParserTests.Helper;
using Contracts.Graph;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;

namespace CodeParserTests.UnitTests.ArchitecturalRules;

[TestFixture]
public class RuleValidationTests
{

    [SetUp]
    public void SetUp()
    {
        _codeGraph = new TestCodeGraph();
    }

    private TestCodeGraph _codeGraph;

    [Test]
    public void DenyRule_ValidateRule_ShouldFindViolations()
    {
        // Arrange
        var businessClass = _codeGraph.CreateClass("MyApp.Business.OrderService");
        var dataClass = _codeGraph.CreateClass("MyApp.Data.OrderRepository");

        // Add forbidden relationship
        businessClass.Relationships.Add(new Relationship(businessClass.Id, dataClass.Id, RelationshipType.Uses));

        var denyRule = new DenyRule
        {
            Source = "MyApp.Business.**",
            Target = "MyApp.Data.**"
        };

        var sourceIds = new HashSet<string> { businessClass.Id };
        var targetIds = new HashSet<string> { dataClass.Id };
        var allRelationships = _codeGraph.GetAllRelationships();

        // Act
        var violations = denyRule.ValidateRule(sourceIds, targetIds, allRelationships);

        // Assert
        Assert.AreEqual(1, violations.Count);
        Assert.AreEqual(businessClass.Id, violations[0].SourceId);
        Assert.AreEqual(dataClass.Id, violations[0].TargetId);
    }

    [Test]
    public void DenyRule_NoViolations_ShouldReturnEmpty()
    {
        // Arrange
        var businessClass = _codeGraph.CreateClass("MyApp.Business.OrderService");
        var serviceClass = _codeGraph.CreateClass("MyApp.Services.EmailService");

        // Add allowed relationship (not to Data layer)
        businessClass.Relationships.Add(new Relationship(businessClass.Id, serviceClass.Id, RelationshipType.Uses));

        var denyRule = new DenyRule();
        var sourceIds = new HashSet<string> { businessClass.Id };
        var targetIds = new HashSet<string> { "NonExistentDataClass" }; // No Data classes
        var allRelationships = _codeGraph.GetAllRelationships();

        // Act
        var violations = denyRule.ValidateRule(sourceIds, targetIds, allRelationships);

        // Assert
        Assert.AreEqual(0, violations.Count);
    }

    [Test]
    public void IsolateRule_ValidateRule_ShouldFindExternalDependencies()
    {
        // Arrange
        var domainClass = _codeGraph.CreateClass("MyApp.Domain.Order");
        var externalClass = _codeGraph.CreateClass("MyApp.Data.Database");

        // Add external dependency (violation)
        domainClass.Relationships.Add(new Relationship(domainClass.Id, externalClass.Id, RelationshipType.Uses));

        var isolateRule = new IsolateRule();
        var sourceIds = new HashSet<string> { domainClass.Id };
        var emptyTargetIds = new HashSet<string>();
        var allRelationships = _codeGraph.GetAllRelationships();

        // Act
        var violations = isolateRule.ValidateRule(sourceIds, emptyTargetIds, allRelationships);

        // Assert
        Assert.AreEqual(1, violations.Count);
        Assert.AreEqual(domainClass.Id, violations[0].SourceId);
        Assert.AreEqual(externalClass.Id, violations[0].TargetId);
    }

    [Test]
    public void IsolateRule_InternalDependencies_ShouldNotViolate()
    {
        // Arrange
        var domainOrder = _codeGraph.CreateClass("MyApp.Domain.Order");
        var domainProduct = _codeGraph.CreateClass("MyApp.Domain.Product");

        // Add internal dependency (allowed)
        domainOrder.Relationships.Add(new Relationship(domainOrder.Id, domainProduct.Id, RelationshipType.Uses));

        var isolateRule = new IsolateRule();
        var sourceIds = new HashSet<string> { domainOrder.Id, domainProduct.Id }; // Both in domain
        var emptyTargetIds = new HashSet<string>();
        var allRelationships = _codeGraph.GetAllRelationships();

        // Act
        var violations = isolateRule.ValidateRule(sourceIds, emptyTargetIds, allRelationships);

        // Assert
        Assert.AreEqual(0, violations.Count);
    }

    [Test]
    public void RestrictRule_ThrowsException_WhenUsedDirectly()
    {
        var restrictRule = new RestrictRule();
        var sourceIds = new HashSet<string>();
        var targetIds = new HashSet<string>();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            restrictRule.ValidateRule(sourceIds, targetIds, []));

        Assert.That(ex.Message, Contains.Substring("RestrictRuleGroup"));
    }

    [Test]
    public void RestrictRuleGroup_ValidateGroup_ShouldFindViolations()
    {
        // Arrange
        var controller = _codeGraph.CreateClass("MyApp.Controllers.OrderController");
        var service = _codeGraph.CreateClass("MyApp.Services.OrderService");
        var dataClass = _codeGraph.CreateClass("MyApp.Data.OrderRepository");

        // Add allowed dependency
        controller.Relationships.Add(new Relationship(controller.Id, service.Id, RelationshipType.Uses));
        // Add forbidden dependency (not in allowed targets)
        controller.Relationships.Add(new Relationship(controller.Id, dataClass.Id, RelationshipType.Uses));

        var restrictRule = new RestrictRule { Source = "Controllers.**", Target = "Services.**" };
        var restrictGroup = new RestrictRuleGroup("Controllers.**", [restrictRule]);
        restrictGroup.AllowedTargetIds = [service.Id]; // Only service allowed

        var sourceIds = new HashSet<string> { controller.Id };
        var allRelationships = _codeGraph.GetAllRelationships();

        // Act
        var violations = restrictGroup.ValidateGroup(sourceIds, allRelationships);

        // Assert
        Assert.AreEqual(1, violations.Count); // Only the Data dependency should violate
        Assert.AreEqual(controller.Id, violations[0].SourceId);
        Assert.AreEqual(dataClass.Id, violations[0].TargetId);
    }

    [Test]
    public void RestrictRuleGroup_OnlyAllowedDependencies_ShouldReturnNoViolations()
    {
        // Arrange
        var controller = _codeGraph.CreateClass("MyApp.Controllers.OrderController");
        var service = _codeGraph.CreateClass("MyApp.Services.OrderService");

        // Add only allowed dependency
        controller.Relationships.Add(new Relationship(controller.Id, service.Id, RelationshipType.Uses));

        var restrictRule = new RestrictRule { Source = "Controllers.**", Target = "Services.**" };
        var restrictGroup = new RestrictRuleGroup("Controllers.**", [restrictRule]);
        restrictGroup.AllowedTargetIds = [service.Id];

        var sourceIds = new HashSet<string> { controller.Id };
        var allRelationships = _codeGraph.GetAllRelationships();

        // Act
        var violations = restrictGroup.ValidateGroup(sourceIds, allRelationships);

        // Assert
        Assert.AreEqual(0, violations.Count);
    }
}