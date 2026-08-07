using System.Text.Json;
using CodeParserTests.Helper;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Sarif;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeGraph.Metrics;

namespace CodeParserTests.UnitTests.ArchitecturalRules;

/// <summary>
///     Covers the SARIF output of the command-line validation: the shape a consumer relies on
///     (result per offending place, repository-relative paths, stable fingerprints) rather than the
///     exact wording of a message.
/// </summary>
[TestFixture]
public class SarifFormatterTests
{
    [SetUp]
    public void SetUp()
    {
        _codeGraph = new TestCodeGraph();
        _metricStore = new MetricStore();
    }

    /// <summary>
    ///     Nothing below these paths has to exist: the formatter never opens a file, it only
    ///     normalizes the paths it is given. They are still derived from the test directory rather
    ///     than written out, so the fixture carries no assumption about the drive layout of the
    ///     machine it runs on.
    /// </summary>
    private static readonly string SourceRoot =
        Path.Combine(TestContext.CurrentContext.TestDirectory, "SarifSourceRoot");

    private static readonly string OutsideRoot =
        Path.Combine(TestContext.CurrentContext.TestDirectory, "SarifOutside");

    private static readonly string RulesFile = Path.Combine(SourceRoot, "architecture.rules.txt");

    private TestCodeGraph _codeGraph = null!;
    private MetricStore _metricStore = null!;

    private static string InRoot(params string[] segments)
    {
        return Path.Combine([SourceRoot, .. segments]);
    }

    private JsonElement Run(string rulesText, string toolVersion = "1.2.3")
    {
        var rules = RuleParser.ParseRules(rulesText);
        var result = RuleEngine.Execute(rules, _codeGraph, _metricStore);

        var context = new SarifContext
        {
            SourceRoot = SourceRoot,
            RulesFile = RulesFile,
            ToolVersion = toolVersion
        };

        var json = SarifFormatter.Format(_codeGraph, result, context);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static JsonElement At(JsonElement log, string path)
    {
        var current = log;
        foreach (var segment in path.Split('.'))
        {
            current = int.TryParse(segment, out var index) ? current[index] : current.GetProperty(segment);
        }

        return current;
    }

    private static JsonElement TheRun(JsonElement log)
    {
        return log.GetProperty("runs")[0];
    }

    private static JsonElement Results(JsonElement log)
    {
        return TheRun(log).GetProperty("results");
    }

    /// <summary>Two forbidden dependencies out of MyApp.Business into MyApp.Data.</summary>
    private (CodeElement OrderLogic, CodeElement Repository) CreateGraphWithTwoDenyViolations()
    {
        var business = _codeGraph.CreateNamespace("MyApp.Business");
        var data = _codeGraph.CreateNamespace("MyApp.Data");

        var orderLogic = _codeGraph.CreateClass("OrderLogic", business);
        var invoiceLogic = _codeGraph.CreateClass("InvoiceLogic", business);
        var repository = _codeGraph.CreateClass("Repository", data);

        orderLogic.SourceLocations.Add(new SourceLocation(InRoot("src", "Business", "OrderLogic.cs"), 5, 1));

        var first = new Relationship(orderLogic.Id, repository.Id, RelationshipType.Uses);
        first.SourceLocations.Add(new SourceLocation(InRoot("src", "Business", "OrderLogic.cs"), 42, 17));
        orderLogic.Relationships.Add(first);

        var second = new Relationship(invoiceLogic.Id, repository.Id, RelationshipType.Uses);
        second.SourceLocations.Add(new SourceLocation(InRoot("src", "Business", "InvoiceLogic.cs"), 12, 9));
        invoiceLogic.Relationships.Add(second);

        return (orderLogic, repository);
    }

    [Test]
    public void Log_HasTheExpectedEnvelope()
    {
        CreateGraphWithTwoDenyViolations();

        var log = Run("DENY MyApp.Business.** -> MyApp.Data.**");

        Assert.That(log.GetProperty("version").GetString(), Is.EqualTo("2.1.0"));
        Assert.That(log.TryGetProperty("$schema", out _), Is.True);
        Assert.That(At(log, "runs.0.tool.driver.name").GetString(), Is.EqualTo("CSharpCodeAnalyst"));
        Assert.That(At(log, "runs.0.columnKind").GetString(), Is.EqualTo("utf16CodeUnits"));
    }

    /// <summary>
    ///     The version the build stamped is written whole, including the commit after the '+' - that is
    ///     what pins a report to the build that produced it.
    /// </summary>
    [Test]
    public void ToolVersion_IsWrittenWholeAndAsSemanticVersionWhenItIsOne()
    {
        var driver = At(Run("MAXLINES = 50", "0.9.0+c1477f57a449"), "runs.0.tool.driver");

        Assert.That(driver.GetProperty("version").GetString(), Is.EqualTo("0.9.0+c1477f57a449"));
        Assert.That(driver.GetProperty("semanticVersion").GetString(), Is.EqualTo("0.9.0"));
    }

    /// <summary>
    ///     A build without an explicit -p:Version stamps the four-part "0.1.0.0", which SemVer does not
    ///     allow. Writing it as semanticVersion anyway would produce a log that fails validation.
    /// </summary>
    [Test]
    public void ToolVersion_ThatIsNotSemantic_OmitsTheSemanticVersion()
    {
        var driver = At(Run("MAXLINES = 50", "0.1.0.0+c1477f57a449"), "runs.0.tool.driver");

        Assert.That(driver.GetProperty("version").GetString(), Is.EqualTo("0.1.0.0+c1477f57a449"));
        Assert.That(driver.TryGetProperty("semanticVersion", out _), Is.False);
    }

    [TestCase("1.0.0-beta.1", "1.0.0-beta.1")]
    [TestCase("1.0.0-rc1+abc", "1.0.0-rc1")]
    [TestCase("1.2", null)]
    [TestCase("0.1.0.0", null)]
    [TestCase("v1.2.3", null)]
    public void SemanticVersion_IsOnlyWrittenForARealSemanticVersion(string version, string? expected)
    {
        var driver = At(Run("MAXLINES = 50", version), "runs.0.tool.driver");

        var actual = driver.TryGetProperty("semanticVersion", out var value) ? value.GetString() : null;
        Assert.That(actual, Is.EqualTo(expected));
    }

    /// <summary>
    ///     One result per offending relationship, not one per rule - that is what makes a consumer
    ///     annotate every offending line instead of a single collective place.
    /// </summary>
    [Test]
    public void DependencyViolation_ProducesOneResultPerRelationship()
    {
        CreateGraphWithTwoDenyViolations();

        var results = Results(Run("DENY MyApp.Business.** -> MyApp.Data.**"));

        Assert.That(results.GetArrayLength(), Is.EqualTo(2));
        foreach (var result in results.EnumerateArray())
        {
            Assert.That(result.GetProperty("ruleId").GetString(), Is.EqualTo("DENY"));
            Assert.That(result.GetProperty("level").GetString(), Is.EqualTo("error"));
        }
    }

    [Test]
    public void RuleDescriptor_IsEmittedOnceAndReferencedByIndex()
    {
        CreateGraphWithTwoDenyViolations();

        var log = Run("DENY MyApp.Business.** -> MyApp.Data.**");
        var rules = At(log, "runs.0.tool.driver.rules");

        Assert.That(rules.GetArrayLength(), Is.EqualTo(1));
        Assert.That(rules[0].GetProperty("id").GetString(), Is.EqualTo("DENY"));

        foreach (var result in Results(log).EnumerateArray())
        {
            Assert.That(result.GetProperty("ruleIndex").GetInt32(), Is.EqualTo(0));
        }
    }

    /// <summary>
    ///     An absolute path from the machine that ran the validation matches no file in the consuming
    ///     system. Everything below the source root has to become relative to it.
    /// </summary>
    [Test]
    public void Location_IsRelativeToTheSourceRoot()
    {
        CreateGraphWithTwoDenyViolations();

        var log = Run("DENY MyApp.Business.** -> MyApp.Data.**");
        var location = At(Results(log)[0], "locations.0.physicalLocation");

        Assert.That(At(location, "artifactLocation.uri").GetString(), Is.EqualTo("src/Business/InvoiceLogic.cs"));
        Assert.That(At(location, "artifactLocation.uriBaseId").GetString(), Is.EqualTo("SRCROOT"));
        Assert.That(At(location, "region.startLine").GetInt32(), Is.EqualTo(12));
        Assert.That(At(location, "region.startColumn").GetInt32(), Is.EqualTo(9));

        // A directory URI - the trailing slash is what makes the relative URIs above resolve against it.
        Assert.That(At(log, "runs.0.originalUriBaseIds.SRCROOT.uri").GetString(),
            Does.StartWith("file:///").And.EndWith("/SarifSourceRoot/"));
    }

    [Test]
    public void Results_AreOrderedByTheNamesOfTheirEnds()
    {
        CreateGraphWithTwoDenyViolations();

        var results = Results(Run("DENY MyApp.Business.** -> MyApp.Data.**"));

        Assert.That(At(results[0], "locations.0.physicalLocation.artifactLocation.uri").GetString(),
            Is.EqualTo("src/Business/InvoiceLogic.cs"));
        Assert.That(At(results[1], "locations.0.physicalLocation.artifactLocation.uri").GetString(),
            Is.EqualTo("src/Business/OrderLogic.cs"));
    }

    /// <summary>
    ///     The rule that produced a finding is one click away, and it is the line that was actually
    ///     written - not the first line of the file.
    /// </summary>
    [Test]
    public void Result_PointsBackAtTheRuleLine()
    {
        CreateGraphWithTwoDenyViolations();

        var log = Run("""
                      // A comment

                      DENY MyApp.Business.** -> MyApp.Data.**
                      """);

        var related = At(Results(log)[0], "relatedLocations.0.physicalLocation");

        Assert.That(At(related, "artifactLocation.uri").GetString(), Is.EqualTo("architecture.rules.txt"));
        Assert.That(At(related, "region.startLine").GetInt32(), Is.EqualTo(3));
    }

    /// <summary>
    ///     The identity of a finding must survive moving code around, otherwise every acknowledged
    ///     alert comes back as a new one on the next commit.
    /// </summary>
    [Test]
    public void Fingerprint_IsIndependentOfFileAndLine()
    {
        var (orderLogic, _) = CreateGraphWithTwoDenyViolations();

        const string rules = "DENY MyApp.Business.** -> MyApp.Data.**";
        var before = Results(Run(rules))[1].GetProperty("partialFingerprints");

        // Same dependency, different place: the file was renamed and the call moved down.
        var relationship = orderLogic.Relationships.Single();
        relationship.SourceLocations.Clear();
        relationship.SourceLocations.Add(new SourceLocation(InRoot("src", "Business", "Renamed.cs"), 987, 3));

        var after = Results(Run(rules))[1].GetProperty("partialFingerprints");

        Assert.That(after.GetProperty(SarifFormatter.FingerprintKey).GetString(),
            Is.EqualTo(before.GetProperty(SarifFormatter.FingerprintKey).GetString()));
    }

    [Test]
    public void Fingerprint_DiffersBetweenDifferentFindings()
    {
        CreateGraphWithTwoDenyViolations();

        var results = Results(Run("DENY MyApp.Business.** -> MyApp.Data.**"));

        var first = At(results[0], $"partialFingerprints.{SarifFormatter.FingerprintKey}").GetString();
        var second = At(results[1], $"partialFingerprints.{SarifFormatter.FingerprintKey}").GetString();

        Assert.That(first, Is.Not.EqualTo(second));
    }

    /// <summary>
    ///     A system metric rule has no place in the code. A result without any location is dropped by
    ///     some consumers, so the rule line becomes the primary location instead of a related one.
    /// </summary>
    [Test]
    public void SystemMetricViolation_IsAnchoredAtTheRuleLine()
    {
        var ns = _codeGraph.CreateNamespace("MyApp.Domain");
        var a = _codeGraph.CreateClass("A", ns);
        var b = _codeGraph.CreateClass("B", ns);

        a.Relationships.Add(new Relationship(a.Id, b.Id, RelationshipType.Uses));
        b.Relationships.Add(new Relationship(b.Id, a.Id, RelationshipType.Uses));

        var results = Results(Run("MAXCYCLICITY = 10"));

        Assert.That(results.GetArrayLength(), Is.EqualTo(1));
        Assert.That(results[0].GetProperty("ruleId").GetString(), Is.EqualTo("MAXCYCLICITY"));
        Assert.That(At(results[0], "locations.0.physicalLocation.artifactLocation.uri").GetString(),
            Is.EqualTo("architecture.rules.txt"));
        Assert.That(At(results[0], "locations.0.physicalLocation.region.startLine").GetInt32(), Is.EqualTo(1));
        Assert.That(results[0].TryGetProperty("relatedLocations", out _), Is.False);

        // Promoted from a related location to the primary one, so its "Rule defined here" caption
        // must be gone - it would read as a claim about where the finding is.
        Assert.That(At(results[0], "locations.0").TryGetProperty("message", out _), Is.False);

        Assert.That(At(results[0], "properties.value").GetDouble(), Is.EqualTo(100.0));
    }

    [Test]
    public void ElementMetricViolation_ProducesOneResultPerElement()
    {
        var ns = _codeGraph.CreateNamespace("MyApp.Business");
        var order = _codeGraph.CreateClass("Order", ns);
        var big = _codeGraph.CreateMethod("Big", order);
        big.SourceLocations.Add(new SourceLocation(InRoot("src", "Business", "Order.cs"), 120, 5));
        _metricStore.Add(big.Id, new MemberMetrics { CodeLines = 80 });

        var results = Results(Run("MAXLINES = 50"));

        Assert.That(results.GetArrayLength(), Is.EqualTo(1));
        Assert.That(results[0].GetProperty("ruleId").GetString(), Is.EqualTo("MAXLINES"));
        Assert.That(At(results[0], "locations.0.physicalLocation.region.startLine").GetInt32(), Is.EqualTo(120));
        Assert.That(At(results[0], "properties.value").GetDouble(), Is.EqualTo(80.0));
        Assert.That(At(results[0], "properties.threshold").GetDouble(), Is.EqualTo(50.0));
    }

    /// <summary>
    ///     A cycle is a property of the group. Splitting it into its edges would report one finding as
    ///     many unrelated alerts.
    /// </summary>
    [Test]
    public void CycleViolation_IsOneResultWithTheParticipantsAsLocations()
    {
        var ns = _codeGraph.CreateNamespace("MyApp.Domain");
        var a = _codeGraph.CreateClass("A", ns);
        var b = _codeGraph.CreateClass("B", ns);

        a.SourceLocations.Add(new SourceLocation(InRoot("src", "Domain", "A.cs"), 3, 1));
        b.SourceLocations.Add(new SourceLocation(InRoot("src", "Domain", "B.cs"), 4, 1));

        a.Relationships.Add(new Relationship(a.Id, b.Id, RelationshipType.Uses));
        b.Relationships.Add(new Relationship(b.Id, a.Id, RelationshipType.Uses));

        var results = Results(Run("NOCYCLES MyApp.Domain"));

        Assert.That(results.GetArrayLength(), Is.EqualTo(1));
        Assert.That(results[0].GetProperty("ruleId").GetString(), Is.EqualTo("NOCYCLES"));
        Assert.That(results[0].GetProperty("locations").GetArrayLength(), Is.EqualTo(2));
        Assert.That(At(results[0], "properties.participantCount").GetInt32(), Is.EqualTo(2));
    }

    /// <summary>
    ///     A rule that matches nothing is a problem with the configuration, not a finding about the
    ///     code. Reporting it as a result would break "no results means the architecture is clean",
    ///     which is the statement the exit code makes.
    /// </summary>
    [Test]
    public void DeadRule_IsANotificationAndNotAResult()
    {
        var log = Run("DENY Nowhere.** -> AlsoNowhere.**");

        Assert.That(Results(log).GetArrayLength(), Is.EqualTo(0));

        var notifications = At(log, "runs.0.invocations.0.toolConfigurationNotifications");
        Assert.That(notifications.GetArrayLength(), Is.GreaterThan(0));
        Assert.That(notifications[0].GetProperty("level").GetString(), Is.EqualTo("warning"));
    }

    [Test]
    public void CleanRun_ProducesAnEmptyResultList()
    {
        _codeGraph.CreateNamespace("MyApp.Business");
        _codeGraph.CreateNamespace("MyApp.Data");

        var log = Run("DENY MyApp.Business.** -> MyApp.Data.**");

        // Present but empty: an absent result list would claim that nothing was analyzed at all.
        Assert.That(TheRun(log).TryGetProperty("results", out var results), Is.True);
        Assert.That(results.GetArrayLength(), Is.EqualTo(0));

        Assert.That(At(log, "runs.0.tool.driver").TryGetProperty("rules", out _), Is.False);
        Assert.That(At(log, "runs.0.invocations.0.executionSuccessful").GetBoolean(), Is.True);
    }

    /// <summary>A parser failure must be visible to a consumer that only ever reads the SARIF file.</summary>
    [Test]
    public void ParserFailure_IsReportedAsANotification()
    {
        var result = new RuleAnalysisResult();
        var context = new SarifContext { RunNotifications = ["The solution did not load cleanly."] };

        var log = JsonDocument.Parse(SarifFormatter.Format(_codeGraph, result, context)).RootElement;
        var notifications = At(log, "runs.0.invocations.0.toolConfigurationNotifications");

        Assert.That(notifications.GetArrayLength(), Is.EqualTo(1));
        Assert.That(At(notifications[0], "message.text").GetString(), Is.EqualTo("The solution did not load cleanly."));
    }

    /// <summary>Without a source root there is nothing to be relative to, so the URI stays absolute.</summary>
    [Test]
    public void FileOutsideTheSourceRoot_KeepsAnAbsoluteUri()
    {
        var business = _codeGraph.CreateNamespace("MyApp.Business");
        var data = _codeGraph.CreateNamespace("MyApp.Data");
        var logic = _codeGraph.CreateClass("Logic", business);
        var repository = _codeGraph.CreateClass("Repository", data);

        var relationship = new Relationship(logic.Id, repository.Id, RelationshipType.Uses);
        relationship.SourceLocations.Add(new SourceLocation(Path.Combine(OutsideRoot, "Generated.cs"), 7, 1));
        logic.Relationships.Add(relationship);

        var log = Run("DENY MyApp.Business.** -> MyApp.Data.**");
        var artifact = At(Results(log)[0], "locations.0.physicalLocation.artifactLocation");

        Assert.That(artifact.GetProperty("uri").GetString(),
            Does.StartWith("file:///").And.EndWith("/SarifOutside/Generated.cs"));
        Assert.That(artifact.TryGetProperty("uriBaseId", out _), Is.False);
    }
}
