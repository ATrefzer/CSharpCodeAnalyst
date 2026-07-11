using CodeParserTests.Helper;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeGraph.Metrics;

namespace CodeParserTests.UnitTests.ArchitecturalRules;

/// <summary>
///     Covers the ALLOW exception rule, the metric rules and the empty-pattern warnings of the rule engine.
/// </summary>
[TestFixture]
public class RuleEngineTests
{

    [SetUp]
    public void SetUp()
    {
        _codeGraph = new TestCodeGraph();
        _metricStore = new MetricStore();
    }

    private TestCodeGraph _codeGraph;
    private MetricStore _metricStore;

    private RuleAnalysisResult Execute(string rulesText)
    {
        var rules = RuleParser.ParseRules(rulesText);
        return RuleEngine.Execute(rules, _codeGraph, _metricStore);
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

    /// <summary>
    ///     Two of the four types form a cycle, so the cyclicity of this graph is 50 percent.
    /// </summary>
    private void CreateGraphWithCyclicityOfFiftyPercent()
    {
        var ns = _codeGraph.CreateNamespace("MyApp.Domain");

        var a = _codeGraph.CreateClass("A", ns);
        var b = _codeGraph.CreateClass("B", ns);
        var c = _codeGraph.CreateClass("C", ns);
        _codeGraph.CreateClass("D", ns);

        a.Relationships.Add(new Relationship(a.Id, b.Id, RelationshipType.Uses));
        b.Relationships.Add(new Relationship(b.Id, a.Id, RelationshipType.Uses));
        c.Relationships.Add(new Relationship(c.Id, a.Id, RelationshipType.Uses));
    }

    [Test]
    public void MaxCyclicity_CyclicityAboveThreshold_ReportsViolation()
    {
        CreateGraphWithCyclicityOfFiftyPercent();

        var result = Execute("MAXCYCLICITY = 40");

        Assert.That(result.Violations, Has.Count.EqualTo(1));
        Assert.That(result.Violations[0].ViolatingRelationships, Is.Empty);
        Assert.That(result.Violations[0].MetricValue, Is.EqualTo(50.0));
        Assert.That(result.Warnings, Is.Empty);
    }

    [Test]
    public void MaxCyclicity_CyclicityEqualsThreshold_IsClean()
    {
        CreateGraphWithCyclicityOfFiftyPercent();

        var result = Execute("MAXCYCLICITY = 50");

        Assert.That(result.Violations, Is.Empty);
    }

    [Test]
    public void MaxCyclicity_AllowRule_DoesNotSuppressMetricViolation()
    {
        CreateGraphWithCyclicityOfFiftyPercent();

        var result = Execute("""
                             MAXCYCLICITY = 10
                             ALLOW: MyApp.Domain.** -> MyApp.Domain.**
                             """);

        Assert.That(result.Violations, Has.Count.EqualTo(1));
    }

    /// <summary>
    ///     Accepting the baseline must relax a violated metric rule to the measured value - and the
    ///     re-validation of the rewritten text must then be clean.
    /// </summary>
    [Test]
    public void MaxCyclicity_AcceptBaseline_RelaxesThresholdToMeasuredValue()
    {
        CreateGraphWithCyclicityOfFiftyPercent();

        const string rulesText = "// A comment\nMAXCYCLICITY = 10\n";
        var violations = Execute(rulesText).Violations;

        var relaxed = BaselineGenerator.RelaxMetricRules(rulesText, violations);

        Assert.That(relaxed, Is.EqualTo("// A comment\nMAXCYCLICITY = 50\n"));
        Assert.That(Execute(relaxed).Violations, Is.Empty);
    }

    /// <summary>
    ///     A measured value that is not representable in the precision of a rule line must be rounded
    ///     up, otherwise the freshly written baseline rule is violated again.
    /// </summary>
    [Test]
    public void MaxCyclicity_AcceptBaseline_RoundsThresholdUp()
    {
        var ns = _codeGraph.CreateNamespace("MyApp.Domain");
        var a = _codeGraph.CreateClass("A", ns);
        var b = _codeGraph.CreateClass("B", ns);
        _codeGraph.CreateClass("C", ns);

        // Two of three types are in a cycle: 66.666... percent.
        a.Relationships.Add(new Relationship(a.Id, b.Id, RelationshipType.Uses));
        b.Relationships.Add(new Relationship(b.Id, a.Id, RelationshipType.Uses));

        const string rulesText = "MAXCYCLICITY = 10";
        var relaxed = BaselineGenerator.RelaxMetricRules(rulesText, Execute(rulesText).Violations);

        Assert.That(relaxed, Is.EqualTo("MAXCYCLICITY = 66.67"));
        Assert.That(Execute(relaxed).Violations, Is.Empty);
    }

    /// <summary>
    ///     "small" and "big" are measured, "abstractMethod" is a method without a body and therefore
    ///     has no entry in the metric store.
    /// </summary>
    private (CodeElement Small, CodeElement Big, CodeElement AbstractMethod) CreateGraphWithMethodMetrics()
    {
        var ns = _codeGraph.CreateNamespace("MyApp.Domain");
        var order = _codeGraph.CreateClass("Order", ns);

        var small = _codeGraph.CreateMethod("Small", order);
        var big = _codeGraph.CreateMethod("Big", order);
        var abstractMethod = _codeGraph.CreateMethod("Abstract", order);

        _metricStore.Add(small.Id, new MemberMetrics { CodeLines = 10 });
        _metricStore.Add(big.Id, new MemberMetrics { CodeLines = 80 });

        return (small, big, abstractMethod);
    }

    [Test]
    public void MaxLines_ReportsOnlyElementsAboveTheThreshold()
    {
        var (_, big, _) = CreateGraphWithMethodMetrics();

        var result = Execute("MAXLINES = 50");

        Assert.That(result.Violations, Has.Count.EqualTo(1));
        var violation = result.Violations[0];
        Assert.That(violation.ViolatingElements, Has.Count.EqualTo(1));
        Assert.That(violation.ViolatingElements[0].Element.Id, Is.EqualTo(big.Id));
        Assert.That(violation.ViolatingElements[0].Value, Is.EqualTo(80.0));
        Assert.That(result.Warnings, Is.Empty);
    }

    /// <summary>
    ///     A method without a body has no metric. It is not applicable, not compliant - and must not
    ///     produce a violation or a warning.
    /// </summary>
    [Test]
    public void MaxLines_ElementWithoutMetric_IsIgnored()
    {
        CreateGraphWithMethodMetrics();

        var result = Execute("MAXLINES = 5");

        Assert.That(result.Violations[0].ViolatingElements, Has.Count.EqualTo(2));
        Assert.That(result.Warnings, Is.Empty);
    }

    [Test]
    public void MaxLines_Pattern_ScopesTheRule()
    {
        CreateGraphWithMethodMetrics();
        var other = _codeGraph.CreateNamespace("MyApp.Legacy");
        var legacyClass = _codeGraph.CreateClass("LegacyService", other);
        var legacyMethod = _codeGraph.CreateMethod("Huge", legacyClass);
        _metricStore.Add(legacyMethod.Id, new MemberMetrics { CodeLines = 500 });

        var result = Execute("MAXLINES: MyApp.Legacy.** = 50");

        Assert.That(result.Violations[0].ViolatingElements, Has.Count.EqualTo(1));
        Assert.That(result.Violations[0].ViolatingElements[0].Element.Id, Is.EqualTo(legacyMethod.Id));
    }

    /// <summary>
    ///     Without source metrics the rule cannot be checked. Reporting nothing would look like a pass.
    /// </summary>
    [Test]
    public void MaxLines_WithoutSourceMetrics_WarnsInsteadOfPassing()
    {
        _codeGraph.CreateNamespace("MyApp.Domain");

        var result = Execute("MAXLINES = 50");

        Assert.That(result.Violations, Is.Empty);
        Assert.That(result.Warnings, Has.Count.EqualTo(1));
    }

    /// <summary>
    ///     Raising the limit to the worst offender would repeal the rule for every other element,
    ///     so a code element metric rule is not part of a baseline.
    /// </summary>
    [Test]
    public void MaxLines_AcceptBaseline_LeavesTheRuleUntouched()
    {
        CreateGraphWithMethodMetrics();

        const string rulesText = "MAXLINES = 50";
        var relaxed = BaselineGenerator.RelaxMetricRules(rulesText, Execute(rulesText).Violations);

        Assert.That(relaxed, Is.EqualTo(rulesText));
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

    /// <summary>
    ///     RESTRICT rules with the same source widen each other and are reported as one violation that
    ///     names the whole allowed set - not as one violation per rule line.
    /// </summary>
    [Test]
    public void Restrict_SameSource_ReportsOneViolationNamingAllTargets()
    {
        var controllers = _codeGraph.CreateNamespace("MyApp.Controllers");
        var services = _codeGraph.CreateNamespace("MyApp.Services");
        var dtos = _codeGraph.CreateNamespace("MyApp.Dtos");
        var data = _codeGraph.CreateNamespace("MyApp.Data");

        var controller = _codeGraph.CreateClass("OrderController", controllers);
        var service = _codeGraph.CreateClass("OrderService", services);
        var dto = _codeGraph.CreateClass("OrderDto", dtos);
        var repository = _codeGraph.CreateClass("Repository", data);

        // The first two are permitted by one rule each, only the repository is a violation.
        controller.Relationships.Add(new Relationship(controller.Id, service.Id, RelationshipType.Uses));
        controller.Relationships.Add(new Relationship(controller.Id, dto.Id, RelationshipType.Uses));
        controller.Relationships.Add(new Relationship(controller.Id, repository.Id, RelationshipType.Uses));

        var result = Execute("""
                             RESTRICT: MyApp.Controllers.** -> MyApp.Services.**
                             RESTRICT: MyApp.Controllers.** -> MyApp.Dtos.**
                             """);

        Assert.That(result.Violations, Has.Count.EqualTo(1));
        var violation = result.Violations[0];
        Assert.That(violation.ViolatingRelationships, Has.Count.EqualTo(1));
        Assert.That(violation.ViolatingRelationships[0].TargetId, Is.EqualTo(repository.Id));

        var group = violation.Rule as RestrictRuleGroup;
        Assert.That(group, Is.Not.Null);
        Assert.That(group!.DisplayName, Is.EqualTo("RESTRICT"));
        Assert.That(group.Targets, Is.EquivalentTo(new[] { "MyApp.Services.**", "MyApp.Dtos.**" }));
    }

    /// <summary>
    ///     RESTRICT rules are grouped on their resolved source sets, not the pattern text, so nested
    ///     patterns like "A.**" and "A.B.**" widen each other - the outer rule must not report the
    ///     inner rule's permitted dependencies as violations.
    /// </summary>
    [Test]
    public void Restrict_OverlappingSources_WidenEachOther()
    {
        var controllers = _codeGraph.CreateNamespace("MyApp.Controllers");
        var admin = _codeGraph.CreateNamespace("MyApp.Controllers.Admin", controllers);
        var services = _codeGraph.CreateNamespace("MyApp.Services");
        var auditing = _codeGraph.CreateNamespace("MyApp.Auditing");
        var data = _codeGraph.CreateNamespace("MyApp.Data");

        var controller = _codeGraph.CreateClass("OrderController", controllers);
        var adminController = _codeGraph.CreateClass("UserController", admin);
        var service = _codeGraph.CreateClass("OrderService", services);
        var audit = _codeGraph.CreateClass("AuditLog", auditing);
        var repository = _codeGraph.CreateClass("Repository", data);

        // Permitted by the outer rule, permitted by the inner rule - and one real violation.
        controller.Relationships.Add(new Relationship(controller.Id, service.Id, RelationshipType.Uses));
        adminController.Relationships.Add(new Relationship(adminController.Id, audit.Id, RelationshipType.Uses));
        controller.Relationships.Add(new Relationship(controller.Id, repository.Id, RelationshipType.Uses));

        var result = Execute("""
                             RESTRICT: MyApp.Controllers.** -> MyApp.Services.**
                             RESTRICT: MyApp.Controllers.Admin.** -> MyApp.Auditing.**
                             """);

        Assert.That(result.Violations, Has.Count.EqualTo(1));
        var violation = result.Violations[0];
        Assert.That(violation.ViolatingRelationships, Has.Count.EqualTo(1));
        Assert.That(violation.ViolatingRelationships[0].TargetId, Is.EqualTo(repository.Id));

        var group = violation.Rule as RestrictRuleGroup;
        Assert.That(group, Is.Not.Null);
        Assert.That(group!.Sources,
            Is.EquivalentTo(new[] { "MyApp.Controllers.**", "MyApp.Controllers.Admin.**" }));
    }

    /// <summary>
    ///     RESTRICT rules whose sources do not overlap must stay separate groups: each source is
    ///     validated only against its own targets.
    /// </summary>
    [Test]
    public void Restrict_DisjointSources_StaySeparateGroups()
    {
        var web = _codeGraph.CreateNamespace("MyApp.Web");
        var jobs = _codeGraph.CreateNamespace("MyApp.Jobs");
        _codeGraph.CreateNamespace("MyApp.Services");
        var data = _codeGraph.CreateNamespace("MyApp.Data");

        var webClass = _codeGraph.CreateClass("HomeController", web);
        var jobClass = _codeGraph.CreateClass("CleanupJob", jobs);
        var repository = _codeGraph.CreateClass("Repository", data);

        // Both sources break their own rule - two violations, one per group.
        webClass.Relationships.Add(new Relationship(webClass.Id, repository.Id, RelationshipType.Uses));
        jobClass.Relationships.Add(new Relationship(jobClass.Id, repository.Id, RelationshipType.Uses));

        var result = Execute("""
                             RESTRICT: MyApp.Web.** -> MyApp.Services.**
                             RESTRICT: MyApp.Jobs.** -> MyApp.Services.**
                             """);

        Assert.That(result.Violations, Has.Count.EqualTo(2));
        Assert.That(result.Warnings, Is.Empty);
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
