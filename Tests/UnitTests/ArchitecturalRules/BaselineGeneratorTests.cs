using CodeParserTests.Helper;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.ArchitecturalRules;

/// <summary>
///     Covers the baseline generation: freezing current violations as ALLOW exceptions.
/// </summary>
[TestFixture]
public class BaselineGeneratorTests
{

    [SetUp]
    public void SetUp()
    {
        _codeGraph = new TestCodeGraph();
    }

    private TestCodeGraph _codeGraph;

    private (RuleAnalysisResult result, string rulesText) Run(string rulesText)
    {
        var rules = RuleParser.ParseRules(rulesText);
        return (RuleEngine.Execute(rules, _codeGraph), rulesText);
    }

    [Test]
    public void Baseline_GeneratesAllowRule_PerViolatingRelationship()
    {
        var business = _codeGraph.CreateNamespace("MyApp.Business");
        var data = _codeGraph.CreateNamespace("MyApp.Data");
        var orderLogic = _codeGraph.CreateClass("OrderLogic", business);
        var repository = _codeGraph.CreateClass("Repository", data);
        orderLogic.Relationships.Add(new Relationship(orderLogic.Id, repository.Id, RelationshipType.Uses));

        var (result, rulesText) = Run("DENY: MyApp.Business.** -> MyApp.Data.**");

        var baseline = BaselineGenerator.GenerateAllowRules(result.Violations, _codeGraph, rulesText);

        // The baseline freezes the exact full paths of the offending elements.
        Assert.That(baseline, Contains.Substring($"ALLOW: {orderLogic.FullName} -> {repository.FullName}"));
    }

    [Test]
    public void Baseline_AppliedRules_SuppressAllViolations()
    {
        var business = _codeGraph.CreateNamespace("MyApp.Business");
        var data = _codeGraph.CreateNamespace("MyApp.Data");
        var orderLogic = _codeGraph.CreateClass("OrderLogic", business);
        var reportLogic = _codeGraph.CreateClass("ReportLogic", business);
        var repository = _codeGraph.CreateClass("Repository", data);
        orderLogic.Relationships.Add(new Relationship(orderLogic.Id, repository.Id, RelationshipType.Uses));
        reportLogic.Relationships.Add(new Relationship(reportLogic.Id, repository.Id, RelationshipType.Uses));

        var originalRules = "DENY: MyApp.Business.** -> MyApp.Data.**";
        var (result, _) = Run(originalRules);
        Assert.That(result.Violations, Has.Count.EqualTo(1), "sanity: one rule, two relationships");

        // Accept the baseline and re-run with the extended rule set.
        var baseline = BaselineGenerator.GenerateAllowRules(result.Violations, _codeGraph, originalRules);
        var newRulesText = originalRules + Environment.NewLine + baseline;

        var (afterResult, _) = Run(newRulesText);

        Assert.That(afterResult.Violations, Is.Empty, "after accepting the baseline nothing must be reported");
        Assert.That(afterResult.Warnings, Is.Empty);
    }

    [Test]
    public void Baseline_OnlyFreezesExisting_NewViolationStillReported()
    {
        var business = _codeGraph.CreateNamespace("MyApp.Business");
        var data = _codeGraph.CreateNamespace("MyApp.Data");
        var orderLogic = _codeGraph.CreateClass("OrderLogic", business);
        var repository = _codeGraph.CreateClass("Repository", data);
        orderLogic.Relationships.Add(new Relationship(orderLogic.Id, repository.Id, RelationshipType.Uses));

        var originalRules = "DENY: MyApp.Business.** -> MyApp.Data.**";
        var (result, _) = Run(originalRules);
        var baseline = BaselineGenerator.GenerateAllowRules(result.Violations, _codeGraph, originalRules);
        var newRulesText = originalRules + Environment.NewLine + baseline;

        // A NEW violation appears later (a second class starts using the data layer).
        var invoiceLogic = _codeGraph.CreateClass("InvoiceLogic", business);
        invoiceLogic.Relationships.Add(new Relationship(invoiceLogic.Id, repository.Id, RelationshipType.Uses));

        var (afterResult, _) = Run(newRulesText);

        Assert.That(afterResult.Violations, Has.Count.EqualTo(1));
        Assert.That(afterResult.Violations[0].ViolatingRelationships, Has.Count.EqualTo(1));
        Assert.That(afterResult.Violations[0].ViolatingRelationships[0].SourceId, Is.EqualTo(invoiceLogic.Id));
    }

    [Test]
    public void Baseline_IsIdempotent_SkipsExistingAllowRules()
    {
        var business = _codeGraph.CreateNamespace("MyApp.Business");
        var data = _codeGraph.CreateNamespace("MyApp.Data");
        var orderLogic = _codeGraph.CreateClass("OrderLogic", business);
        var repository = _codeGraph.CreateClass("Repository", data);
        orderLogic.Relationships.Add(new Relationship(orderLogic.Id, repository.Id, RelationshipType.Uses));

        var originalRules = "DENY: MyApp.Business.** -> MyApp.Data.**";
        var (result, _) = Run(originalRules);

        var firstBaseline = BaselineGenerator.GenerateAllowRules(result.Violations, _codeGraph, originalRules);
        var textAfterFirst = originalRules + Environment.NewLine + firstBaseline;

        // Running the generator again against the already-baselined text must add nothing.
        var secondBaseline = BaselineGenerator.GenerateAllowRules(result.Violations, _codeGraph, textAfterFirst);

        Assert.That(secondBaseline, Is.Empty);
    }

    [Test]
    public void Baseline_AddsOriginRuleComment()
    {
        var business = _codeGraph.CreateNamespace("MyApp.Business");
        var data = _codeGraph.CreateNamespace("MyApp.Data");
        var orderLogic = _codeGraph.CreateClass("OrderLogic", business);
        var repository = _codeGraph.CreateClass("Repository", data);
        orderLogic.Relationships.Add(new Relationship(orderLogic.Id, repository.Id, RelationshipType.Uses));

        var originalRules = "DENY: MyApp.Business.** -> MyApp.Data.**";
        var (result, _) = Run(originalRules);

        var baseline = BaselineGenerator.GenerateAllowRules(result.Violations, _codeGraph, originalRules);

        Assert.That(baseline, Contains.Substring("// DENY: MyApp.Business.** -> MyApp.Data.**"));
    }

    [Test]
    public void Baseline_NoViolations_ReturnsEmpty()
    {
        var baseline = BaselineGenerator.GenerateAllowRules([], _codeGraph, "");
        Assert.That(baseline, Is.Empty);
    }

    [Test]
    public void Baseline_SuppressesEveryOverloadSharingAFullName()
    {
        // Regression: two overloads share one full path, so the baseline emits a single ALLOW
        // line. That line must suppress BOTH violations, not just an arbitrary one.
        var business = _codeGraph.CreateNamespace("MyApp.Business");
        var data = _codeGraph.CreateNamespace("MyApp.Data");
        var repository = _codeGraph.CreateClass("Repository", data);

        var overload1 = new CodeElement("m1", CodeElementType.Method, "Save", "MyApp.Business.Service.Save", business);
        var overload2 = new CodeElement("m2", CodeElementType.Method, "Save", "MyApp.Business.Service.Save", business);
        _codeGraph.Nodes["m1"] = overload1;
        _codeGraph.Nodes["m2"] = overload2;
        business.Children.Add(overload1);
        business.Children.Add(overload2);
        overload1.Relationships.Add(new Relationship("m1", repository.Id, RelationshipType.Uses));
        overload2.Relationships.Add(new Relationship("m2", repository.Id, RelationshipType.Uses));

        var originalRules = "DENY: MyApp.Business.** -> MyApp.Data.**";
        var (result, _) = Run(originalRules);
        Assert.That(result.Violations.Sum(v => v.ViolatingRelationships.Count), Is.EqualTo(2),
            "sanity: both overloads violate");

        var baseline = BaselineGenerator.GenerateAllowRules(result.Violations, _codeGraph, originalRules);
        var (afterResult, _) = Run(originalRules + Environment.NewLine + baseline);

        Assert.That(afterResult.Violations, Is.Empty, "the single ALLOW line must cover every overload");
    }
}
