using CodeGraph.Graph;
using CodeParserTests.Helper;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;

namespace CodeParserTests.UnitTests.ArchitecturalRules;

[TestFixture]
public class AnalyzerIntegrationTests
{

    [SetUp]
    public void SetUp()
    {
        _codeGraph = new TestCodeGraph();
    }

    private TestCodeGraph _codeGraph;

    private static List<Violation> ExecuteRulesAnalysis(string rulesText, CodeGraph.Graph.CodeGraph graph)
    {
        var rules = RuleParser.ParseRules(rulesText);
        var violations = new List<Violation>();
        var allRelationships = graph.GetAllRelationships().ToList();

        // Group rules by type and source (matching the Analyzer logic)
        var denyRules = rules.OfType<DenyRule>().ToList();
        var isolateRules = rules.OfType<IsolateRule>().ToList();
        var restrictRules = rules.OfType<RestrictRule>().ToList();

        // Process DENY rules
        foreach (var denyRule in denyRules)
        {
            var sourceIds = PatternMatcher.ResolvePattern(denyRule.Source, graph);
            var targetIds = PatternMatcher.ResolvePattern(denyRule.Target, graph);
            var ruleViolations = denyRule.ValidateRule(sourceIds, targetIds, allRelationships);
            if (ruleViolations.Count > 0)
            {
                violations.Add(new Violation(denyRule, ruleViolations));
            }
        }

        // Process ISOLATE rules
        foreach (var isolateRule in isolateRules)
        {
            var sourceIds = PatternMatcher.ResolvePattern(isolateRule.Source, graph);
            var emptyTargetIds = new HashSet<string>();
            var ruleViolations = isolateRule.ValidateRule(sourceIds, emptyTargetIds, allRelationships);
            if (ruleViolations.Count > 0)
            {
                violations.Add(new Violation(isolateRule, ruleViolations));
            }
        }

        // Process RESTRICT rules (group by source)
        var restrictGroups = restrictRules.GroupBy(r => r.Source).ToList();
        foreach (var group in restrictGroups)
        {
            var restrictGroup = new RestrictRuleGroup(group.Key, group);
            var sourceIds = PatternMatcher.ResolvePattern(group.Key, graph);

            var allowedTargetIds = new HashSet<string>();
            foreach (var restrictRule in group)
            {
                var targetIds = PatternMatcher.ResolvePattern(restrictRule.Target, graph);
                allowedTargetIds.UnionWith(targetIds);
            }

            restrictGroup.AllowedTargetIds = allowedTargetIds;
            var groupViolations = restrictGroup.ValidateGroup(sourceIds, allRelationships);
            if (groupViolations.Count > 0)
            {
                violations.Add(new Violation(group.First(), groupViolations));
            }
        }

        return violations;
    }

    [Test]
    public void FullWorkflow_ComplexScenario_ShouldDetectAllViolations()
    {
        // Arrange - Create a complex code structure
        var controllers = _codeGraph.CreateNamespace("MyApp.Controllers");
        var services = _codeGraph.CreateNamespace("MyApp.Services");
        var business = _codeGraph.CreateNamespace("MyApp.Business");
        var data = _codeGraph.CreateNamespace("MyApp.Data");
        var domain = _codeGraph.CreateNamespace("MyApp.Domain");

        var orderController = _codeGraph.CreateClass("OrderController", controllers);
        var userController = _codeGraph.CreateClass("UserController", controllers);

        var orderService = _codeGraph.CreateClass("OrderService", services);
        var emailService = _codeGraph.CreateClass("EmailService", services);

        var orderBusiness = _codeGraph.CreateClass("OrderLogic", business);
        var orderRepository = _codeGraph.CreateClass("OrderRepository", data);

        var orderEntity = _codeGraph.CreateClass("Order", domain);
        var productEntity = _codeGraph.CreateClass("Product", domain);

        // Add relationships that will create violations
        orderController.Relationships.Add(new Relationship(orderController.Id, orderService.Id, RelationshipType.Uses)); // Allowed
        orderController.Relationships.Add(new Relationship(orderController.Id, orderRepository.Id, RelationshipType.Uses)); // RESTRICT violation
        userController.Relationships.Add(new Relationship(userController.Id, orderRepository.Id, RelationshipType.Uses)); // RESTRICT violation

        orderBusiness.Relationships.Add(new Relationship(orderBusiness.Id, orderRepository.Id, RelationshipType.Uses)); // DENY violation

        orderEntity.Relationships.Add(new Relationship(orderEntity.Id, orderRepository.Id, RelationshipType.Uses)); // ISOLATE violation
        productEntity.Relationships.Add(new Relationship(productEntity.Id, orderEntity.Id, RelationshipType.Uses)); // Allowed (internal)

        // Define rules that will detect these violations
        var rulesText = """
                        DENY: MyApp.Business.** -> MyApp.Data.**
                        RESTRICT: MyApp.Controllers.** -> MyApp.Services.**
                        ISOLATE: MyApp.Domain.**
                        """;

        // Act - Execute the full analyzer workflow
        var results = ExecuteRulesAnalysis(rulesText, _codeGraph);

        // Assert - Verify all expected violations are detected
        Assert.AreEqual(3, results.Count); // 3 different rule types should find violations

        // Check DENY violation
        var denyViolation = results.FirstOrDefault(v => v.Rule.RuleText.Contains("DENY"));
        Assert.IsNotNull(denyViolation);
        Assert.AreEqual(1, denyViolation.ViolatingRelationships.Count);
        Assert.AreEqual(orderBusiness.Id, denyViolation.ViolatingRelationships[0].SourceId);
        Assert.AreEqual(orderRepository.Id, denyViolation.ViolatingRelationships[0].TargetId);

        // Check RESTRICT violations (should have 2)
        var restrictViolations = results.Where(v => v.Rule.RuleText.Contains("RESTRICT")).ToList();
        Assert.AreEqual(1, restrictViolations.Count);
        Assert.AreEqual(2, restrictViolations[0].ViolatingRelationships.Count);

        // Check ISOLATE violation
        var isolateViolation = results.FirstOrDefault(v => v.Rule.RuleText.Contains("ISOLATE"));
        Assert.IsNotNull(isolateViolation);
        Assert.AreEqual(1, isolateViolation.ViolatingRelationships.Count);
        Assert.AreEqual(orderEntity.Id, isolateViolation.ViolatingRelationships[0].SourceId);
        Assert.AreEqual(orderRepository.Id, isolateViolation.ViolatingRelationships[0].TargetId);
    }

    [Test]
    public void FullWorkflow_NoViolations_ShouldReturnEmptyResults()
    {
        // Arrange - Create structure with no violations
        var controllers = _codeGraph.CreateNamespace("MyApp.Controllers");
        var services = _codeGraph.CreateNamespace("MyApp.Services");
        var domain = _codeGraph.CreateNamespace("MyApp.Domain");

        var orderController = _codeGraph.CreateClass("OrderController", controllers);
        var orderService = _codeGraph.CreateClass("OrderService", services);
        var orderEntity = _codeGraph.CreateClass("Order", domain);
        var productEntity = _codeGraph.CreateClass("Product", domain);

        // Add only allowed relationships
        orderController.Relationships.Add(new Relationship(orderController.Id, orderService.Id, RelationshipType.Uses));
        orderEntity.Relationships.Add(new Relationship(orderEntity.Id, productEntity.Id, RelationshipType.Uses)); // Internal domain

        var rulesText = """
                        DENY: MyApp.Business.** -> MyApp.Data.**
                        RESTRICT: MyApp.Controllers.** -> MyApp.Services.**
                        ISOLATE: MyApp.Domain.**
                        """;

        // Act
        var results = ExecuteRulesAnalysis(rulesText, _codeGraph);

        // Assert
        Assert.AreEqual(0, results.Count);
    }

    [Test]
    public void FullWorkflow_InvalidRuleText_ShouldThrowException()
    {
        // Arrange
        var invalidRulesText = """
                               DENY: Valid.** -> Rule.**
                               INVALID: Wrong syntax here
                               ISOLATE: Another.Valid.**
                               """;

        // Act & Assert
        var ex = Assert.Throws<FormatException>(() => ExecuteRulesAnalysis(invalidRulesText, _codeGraph));
        Assert.That(ex.Message, Contains.Substring("line 2"));
    }

    [Test]
    public void FullWorkflow_EmptyRules_ShouldReturnEmptyResults()
    {
        // Arrange
        _codeGraph.CreateClass("MyApp.SomeClass");

        // Act
        var results = ExecuteRulesAnalysis("", _codeGraph);

        // Assert
        Assert.AreEqual(0, results.Count);
    }

    [Test]
    public void FullWorkflow_RuleGrouping_ShouldCombineRestrictRules()
    {
        // Arrange - Test that multiple RESTRICT rules with same source are grouped
        var controllers = _codeGraph.CreateNamespace("MyApp.Controllers");
        var services = _codeGraph.CreateNamespace("MyApp.Services");
        var utilities = _codeGraph.CreateNamespace("MyApp.Utilities");
        var data = _codeGraph.CreateNamespace("MyApp.Data");

        var controller = _codeGraph.CreateClass("MyApp.Controllers.TestController", controllers);
        var service = _codeGraph.CreateClass("MyApp.Services.TestService", services);
        var utility = _codeGraph.CreateClass("MyApp.Utilities.Helper", utilities);
        var repository = _codeGraph.CreateClass("MyApp.Data.Repository", data);

        // Add relationships
        controller.Relationships.Add(new Relationship(controller.Id, service.Id, RelationshipType.Uses)); // Allowed by first rule
        controller.Relationships.Add(new Relationship(controller.Id, utility.Id, RelationshipType.Uses)); // Allowed by second rule
        controller.Relationships.Add(new Relationship(controller.Id, repository.Id, RelationshipType.Uses)); // Violation (not in either rule)

        var rulesText = """
                        RESTRICT: MyApp.Controllers.** -> MyApp.Services.**
                        RESTRICT: MyApp.Controllers.** -> MyApp.Utilities.**
                        """;

        // Act
        var results = ExecuteRulesAnalysis(rulesText, _codeGraph);

        // Assert - Should only have 1 violation (the Data dependency)
        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(controller.Id, results[0].ViolatingRelationships[0].SourceId);
        Assert.AreEqual(repository.Id, results[0].ViolatingRelationships[0].TargetId);
    }

    [Test]
    public void FullWorkflow_PatternMatching_ShouldResolveComplexHierarchies()
    {
        // Arrange - Create nested namespace structure
        var myApp = _codeGraph.CreateNamespace("MyApp");
        var business = _codeGraph.CreateNamespace("MyApp.Business", myApp);
        var businessServices = _codeGraph.CreateNamespace("MyApp.Business.Services", business);
        var orderService = _codeGraph.CreateClass("MyApp.Business.Services.OrderService", businessServices);

        var data = _codeGraph.CreateNamespace("MyApp.Data", myApp);
        var repository = _codeGraph.CreateClass("Repository", data);

        // Add violation
        orderService.Relationships.Add(new Relationship(orderService.Id, repository.Id, RelationshipType.Uses));

        // Test recursive pattern matching
        var rulesText = "DENY: MyApp.Business.** -> MyApp.Data.**";

        // Act
        var results = ExecuteRulesAnalysis(rulesText, _codeGraph);

        // Assert - Should detect violation despite nested structure
        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(orderService.Id, results[0].ViolatingRelationships[0].SourceId);
        Assert.AreEqual(repository.Id, results[0].ViolatingRelationships[0].TargetId);
    }
}