using CodeParserTests.Helper;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.ArchitecturalRules;

/// <summary>
///     Covers the ALLOW exception rule and the empty-pattern warnings of the rule engine.
/// </summary>
[TestFixture]
public class RuleEngineTests
{

    [SetUp]
    public void SetUp()
    {
        _codeGraph = new TestCodeGraph();
    }

    private TestCodeGraph _codeGraph;

    private RuleAnalysisResult Execute(string rulesText)
    {
        var rules = RuleParser.ParseRules(rulesText);
        return RuleEngine.Execute(rules, _codeGraph);
    }

    [Test]
    public void Allow_SuppressesDenyViolation()
    {
        var business = _codeGraph.CreateNamespace("MyApp.Business");
        var reporting = _codeGraph.CreateNamespace("MyApp.Business.Reporting", business);
        var data = _codeGraph.CreateNamespace("MyApp.Data");

        var reportGenerator = _codeGraph.CreateClass("ReportGenerator", reporting);
        var repository = _codeGraph.CreateClass("Repository", data);

        reportGenerator.Relationships.Add(new Relationship(reportGenerator.Id, repository.Id, RelationshipType.Uses));

        var result = Execute("""
                             DENY: MyApp.Business.** -> MyApp.Data.**
                             ALLOW: MyApp.Business.Reporting.** -> MyApp.Data.**
                             """);

        Assert.That(result.Violations, Is.Empty);
        Assert.That(result.Warnings, Is.Empty);
    }

    [Test]
    public void Allow_KeepsViolationsNotCoveredByException()
    {
        var business = _codeGraph.CreateNamespace("MyApp.Business");
        var reporting = _codeGraph.CreateNamespace("MyApp.Business.Reporting", business);
        var data = _codeGraph.CreateNamespace("MyApp.Data");

        var orderLogic = _codeGraph.CreateClass("OrderLogic", business);
        var reportGenerator = _codeGraph.CreateClass("ReportGenerator", reporting);
        var repository = _codeGraph.CreateClass("Repository", data);

        // Both access the data layer, but only the reporting subtree is excepted.
        orderLogic.Relationships.Add(new Relationship(orderLogic.Id, repository.Id, RelationshipType.Uses));
        reportGenerator.Relationships.Add(new Relationship(reportGenerator.Id, repository.Id, RelationshipType.Uses));

        var result = Execute("""
                             DENY: MyApp.Business.** -> MyApp.Data.**
                             ALLOW: MyApp.Business.Reporting.** -> MyApp.Data.**
                             """);

        Assert.That(result.Violations, Has.Count.EqualTo(1));
        Assert.That(result.Violations[0].ViolatingRelationships, Has.Count.EqualTo(1));
        Assert.That(result.Violations[0].ViolatingRelationships[0].SourceId, Is.EqualTo(orderLogic.Id));
    }

    [Test]
    public void Allow_SuppressesRestrictViolation()
    {
        var controllers = _codeGraph.CreateNamespace("MyApp.Controllers");
        var services = _codeGraph.CreateNamespace("MyApp.Services");
        var data = _codeGraph.CreateNamespace("MyApp.Data");

        var controller = _codeGraph.CreateClass("OrderController", controllers);
        var service = _codeGraph.CreateClass("OrderService", services);
        var repository = _codeGraph.CreateClass("Repository", data);

        controller.Relationships.Add(new Relationship(controller.Id, service.Id, RelationshipType.Uses));
        controller.Relationships.Add(new Relationship(controller.Id, repository.Id, RelationshipType.Uses));

        var result = Execute("""
                             RESTRICT: MyApp.Controllers.** -> MyApp.Services.**
                             ALLOW: MyApp.Controllers.** -> MyApp.Data.**
                             """);

        Assert.That(result.Violations, Is.Empty);
    }

    [Test]
    public void Allow_SuppressesIsolateViolation()
    {
        var domain = _codeGraph.CreateNamespace("MyApp.Domain");
        var shared = _codeGraph.CreateNamespace("MyApp.SharedKernel");

        var order = _codeGraph.CreateClass("Order", domain);
        var valueObject = _codeGraph.CreateClass("Money", shared);

        order.Relationships.Add(new Relationship(order.Id, valueObject.Id, RelationshipType.Uses));

        var result = Execute("""
                             ISOLATE: MyApp.Domain.**
                             ALLOW: MyApp.Domain.** -> MyApp.SharedKernel.**
                             """);

        Assert.That(result.Violations, Is.Empty);
    }

    [Test]
    public void Allow_AloneProducesNoViolations()
    {
        var business = _codeGraph.CreateNamespace("MyApp.Business");
        var data = _codeGraph.CreateNamespace("MyApp.Data");

        var orderLogic = _codeGraph.CreateClass("OrderLogic", business);
        var repository = _codeGraph.CreateClass("Repository", data);

        orderLogic.Relationships.Add(new Relationship(orderLogic.Id, repository.Id, RelationshipType.Uses));

        var result = Execute("ALLOW: MyApp.Business.** -> MyApp.Data.**");

        Assert.That(result.Violations, Is.Empty);
        Assert.That(result.Warnings, Is.Empty);
    }

    [Test]
    public void EmptySourcePattern_ProducesWarningAndNoViolations()
    {
        var business = _codeGraph.CreateNamespace("MyApp.Business");
        var data = _codeGraph.CreateNamespace("MyApp.Data");
        var orderLogic = _codeGraph.CreateClass("OrderLogic", business);
        var repository = _codeGraph.CreateClass("Repository", data);
        orderLogic.Relationships.Add(new Relationship(orderLogic.Id, repository.Id, RelationshipType.Uses));

        // Typo in the source pattern - the rule silently matched nothing before.
        var result = Execute("DENY: MyApp.Bussiness.** -> MyApp.Data.**");

        Assert.That(result.Violations, Is.Empty);
        Assert.That(result.Warnings, Has.Count.EqualTo(1));
        Assert.That(result.Warnings[0], Contains.Substring("MyApp.Bussiness.**"));
    }

    [Test]
    public void EmptyTargetPattern_ProducesWarning()
    {
        _codeGraph.CreateNamespace("MyApp.Business");

        var result = Execute("DENY: MyApp.Business.** -> MyApp.DoesNotExist.**");

        Assert.That(result.Warnings, Has.Count.EqualTo(1));
        Assert.That(result.Warnings[0], Contains.Substring("MyApp.DoesNotExist.**"));
    }

    [Test]
    public void EmptyAllowPattern_ProducesWarning_AndDenyStillApplies()
    {
        var business = _codeGraph.CreateNamespace("MyApp.Business");
        var data = _codeGraph.CreateNamespace("MyApp.Data");
        var orderLogic = _codeGraph.CreateClass("OrderLogic", business);
        var repository = _codeGraph.CreateClass("Repository", data);
        orderLogic.Relationships.Add(new Relationship(orderLogic.Id, repository.Id, RelationshipType.Uses));

        // The exception has a typo, so it must not suppress anything - and the user must be told.
        var result = Execute("""
                             DENY: MyApp.Business.** -> MyApp.Data.**
                             ALLOW: MyApp.Bussiness.** -> MyApp.Data.**
                             """);

        Assert.That(result.Violations, Has.Count.EqualTo(1));
        Assert.That(result.Warnings, Has.Count.EqualTo(1));
        Assert.That(result.Warnings[0], Contains.Substring("MyApp.Bussiness.**"));
    }

    [Test]
    public void DuplicateEmptyPatterns_ReportedOnlyOnce()
    {
        _codeGraph.CreateNamespace("MyApp.Business");

        var result = Execute("""
                             DENY: MyApp.Business.** -> MyApp.DoesNotExist.**
                             DENY: MyApp.Business.** -> MyApp.DoesNotExist.**
                             """);

        Assert.That(result.Warnings, Has.Count.EqualTo(1));
    }

    [Test]
    public void MatchingPatterns_ProduceNoWarnings()
    {
        var business = _codeGraph.CreateNamespace("MyApp.Business");
        var data = _codeGraph.CreateNamespace("MyApp.Data");
        var orderLogic = _codeGraph.CreateClass("OrderLogic", business);
        var repository = _codeGraph.CreateClass("Repository", data);
        orderLogic.Relationships.Add(new Relationship(orderLogic.Id, repository.Id, RelationshipType.Uses));

        var result = Execute("DENY: MyApp.Business.** -> MyApp.Data.**");

        Assert.That(result.Warnings, Is.Empty);
        Assert.That(result.Violations, Has.Count.EqualTo(1));
    }
}
