using CodeParserTests.Helper;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeGraph.Metrics;

namespace CodeParserTests.UnitTests.ArchitecturalRules;

/// <summary>
///     Covers the "remove empty rules" cleanup: it must drop only rules that currently have no
///     effect, and never change the analysis result.
/// </summary>
[TestFixture]
public class RuleCleanerTests
{

    [SetUp]
    public void SetUp()
    {
        _codeGraph = new TestCodeGraph();
        var business = _codeGraph.CreateNamespace("MyApp.Business");
        var data = _codeGraph.CreateNamespace("MyApp.Data");
        _orderLogic = _codeGraph.CreateClass("OrderLogic", business);
        _repository = _codeGraph.CreateClass("Repository", data);
        _orderLogic.Relationships.Add(new Relationship(_orderLogic.Id, _repository.Id, RelationshipType.Uses));
    }

    private TestCodeGraph _codeGraph;
    private CodeElement _orderLogic;
    private CodeElement _repository;

    [Test]
    public void RemovesDeny_WithUnmatchedSource()
    {
        var (cleaned, removed) = RuleCleaner.RemoveUnusedRules(
            "DENY MyApp.DoesNotExist.** -> MyApp.Data.**", _codeGraph);

        Assert.That(removed, Is.EqualTo(1));
        Assert.That(cleaned.Trim(), Is.Empty);
    }

    [Test]
    public void RemovesDeny_WithUnmatchedTarget()
    {
        var (cleaned, removed) = RuleCleaner.RemoveUnusedRules(
            "DENY MyApp.Business.** -> MyApp.Nope.**", _codeGraph);

        Assert.That(removed, Is.EqualTo(1));
        Assert.That(cleaned.Trim(), Is.Empty);
    }

    [Test]
    public void RemovesIsolate_WithUnmatchedSource()
    {
        var (_, removed) = RuleCleaner.RemoveUnusedRules("ISOLATE MyApp.Nope.**", _codeGraph);
        Assert.That(removed, Is.EqualTo(1));
    }

    [Test]
    public void RemovesAllow_WithUnmatchedSide()
    {
        var (_, removed) = RuleCleaner.RemoveUnusedRules(
            "ALLOW MyApp.Business.** -> MyApp.Nope.**", _codeGraph);
        Assert.That(removed, Is.EqualTo(1));
    }

    [Test]
    public void KeepsRestrict_WithUnmatchedTarget()
    {
        // A RESTRICT with an unmatched target still forbids all external dependencies of its
        // source, so it must NOT be removed (removing it would hide violations).
        var rulesText = "RESTRICT MyApp.Business.** -> MyApp.Nope.**";

        var (cleaned, removed) = RuleCleaner.RemoveUnusedRules(rulesText, _codeGraph);

        Assert.That(removed, Is.EqualTo(0));
        Assert.That(cleaned, Is.EqualTo(rulesText));
    }

    [Test]
    public void RemovesRestrict_WithUnmatchedSource()
    {
        var (_, removed) = RuleCleaner.RemoveUnusedRules(
            "RESTRICT MyApp.Nope.** -> MyApp.Data.**", _codeGraph);
        Assert.That(removed, Is.EqualTo(1));
    }

    [Test]
    public void KeepsMatchingRule()
    {
        var rulesText = "DENY MyApp.Business.** -> MyApp.Data.**";
        var (cleaned, removed) = RuleCleaner.RemoveUnusedRules(rulesText, _codeGraph);

        Assert.That(removed, Is.EqualTo(0));
        Assert.That(cleaned, Is.EqualTo(rulesText));
    }

    [Test]
    public void PreservesCommentsBlankLinesAndInvalidLines()
    {
        var rulesText =
            "// a comment\n" +
            "\n" +
            "DENY MyApp.Nope.** -> MyApp.Data.**\n" +
            "this is not a valid rule\n" +
            "DENY MyApp.Business.** -> MyApp.Data.**";

        var (cleaned, removed) = RuleCleaner.RemoveUnusedRules(rulesText, _codeGraph);

        Assert.That(removed, Is.EqualTo(1));
        Assert.That(cleaned, Does.Contain("// a comment"));
        Assert.That(cleaned, Does.Contain("this is not a valid rule"));
        Assert.That(cleaned, Does.Contain("DENY MyApp.Business.** -> MyApp.Data.**"));
        Assert.That(cleaned, Does.Not.Contain("MyApp.Nope"));
    }

    [Test]
    public void CleanupDoesNotChangeAnalysisResult()
    {
        // Strong invariant: removing empty rules must leave the violation set identical.
        var rulesText =
            "DENY MyApp.Business.** -> MyApp.Data.**\n" +
            "DENY MyApp.Nope.** -> MyApp.Data.**\n" +
            "ALLOW MyApp.Ghost.** -> MyApp.Data.**";

        var before = RuleEngine.Execute(RuleParser.ParseRules(rulesText), _codeGraph, new MetricStore());

        var (cleaned, removed) = RuleCleaner.RemoveUnusedRules(rulesText, _codeGraph);
        var after = RuleEngine.Execute(RuleParser.ParseRules(cleaned), _codeGraph, new MetricStore());

        Assert.That(removed, Is.EqualTo(2));
        Assert.That(after.Violations.Count, Is.EqualTo(before.Violations.Count));
        Assert.That(after.Warnings, Is.Empty, "cleaned rules must no longer warn");
    }

    [Test]
    public void EmptyText_ReturnsUnchanged()
    {
        var (cleaned, removed) = RuleCleaner.RemoveUnusedRules("", _codeGraph);
        Assert.That(removed, Is.EqualTo(0));
        Assert.That(cleaned, Is.Empty);
    }
}
