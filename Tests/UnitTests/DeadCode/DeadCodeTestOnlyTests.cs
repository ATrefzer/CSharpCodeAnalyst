using CodeParserTests.Helper;
using CSharpCodeAnalyst.CodeGraph.Algorithms.DeadCode;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.DeadCode;

/// <summary>
///     Code that only the tests still use. The rule exists because without it, analyzing the tests along
///     with the production code hides exactly these elements - they look referenced.
/// </summary>
[TestFixture]
public class DeadCodeTestOnlyTests
{
    [SetUp]
    public void SetUp()
    {
        _graph = new TestCodeGraph();
        _production = _graph.CreateAssembly("Production");
        _tests = _graph.CreateAssembly("Tests");
    }

    private TestCodeGraph _graph = null!;
    private CodeElement _production = null!;
    private CodeElement _tests = null!;

    private void Rel(CodeElement source, CodeElement target, RelationshipType type)
    {
        source.Relationships.Add(new Relationship(source.Id, target.Id, type));
    }

    /// <summary>A test class - the attribute is what makes it a test type.</summary>
    private CodeElement CreateTestClass(string id)
    {
        var element = _graph.CreateClass(id, _tests);
        element.Attributes.Add("TestFixture");
        return element;
    }

    private string[] Reported()
    {
        return DeadCodeAnalysis.Calculate(_graph).Select(f => f.Element.FullName).ToArray();
    }

    private DeadCodeFinding FindingFor(CodeElement element)
    {
        return DeadCodeAnalysis.Calculate(_graph).Single(f => f.Element.Id == element.Id);
    }

    [Test]
    public void Calculate_ClassOnlyUsedByATest_IsReportedWithTheTestAsReference()
    {
        var helper = _graph.CreateClass("Helper", _production);
        var test = CreateTestClass("HelperTests");
        Rel(test, helper, RelationshipType.Uses);

        var finding = FindingFor(helper);

        Assert.Multiple(() =>
        {
            Assert.That(finding.Hints.HasFlag(DeadCodeHint.UsedOnlyByTests), Is.True);
            Assert.That(finding.TestReferences.Select(m => m.FullName), Is.EqualTo(new[] { "HelperTests" }));
        });
    }

    [Test]
    public void Calculate_ClassUsedByProductionAndByATest_IsNotReported()
    {
        var helper = _graph.CreateClass("Helper", _production);
        var caller = _graph.CreateClass("Caller", _production);
        Rel(caller, helper, RelationshipType.Uses);
        Rel(CreateTestClass("HelperTests"), helper, RelationshipType.Uses);

        Assert.That(Reported(), Does.Not.Contain("Helper"));
    }

    /// <summary>
    ///     The accepted price of the type granularity: a helper class outside the fixtures carries no
    ///     attribute, so to the analysis it is production code that only tests use. The statement is
    ///     true - the helper goes when the tests go - it is just noise when the helper is doing its job.
    /// </summary>
    [Test]
    public void Calculate_TestHelperClassUsedByTests_IsReportedAsUsedOnlyByTests()
    {
        var helper = _graph.CreateClass("TestDataBuilder", _tests);
        Rel(CreateTestClass("SomeTests"), helper, RelationshipType.Uses);

        Assert.That(FindingFor(helper).Hints.HasFlag(DeadCodeHint.UsedOnlyByTests), Is.True);
    }

    /// <summary>
    ///     Inside a fixture the rule stays off: the helper members and nested fakes are what the tests
    ///     are made of. Only the fixture itself is reported, as unreferenced test code like always.
    /// </summary>
    [Test]
    public void Calculate_HelpersInsideAFixture_AreNotReportedSeparately()
    {
        var fixture = CreateTestClass("SomeTests");
        var test = _graph.CreateMethod("SomeTests.ShouldWork", fixture);
        test.Attributes.Add("Test");

        var helper = _graph.CreateMethod("SomeTests.CreateSut", fixture);
        var fake = _graph.CreateClass("SomeTests.FakeService", fixture);
        var fakeMethod = _graph.CreateMethod("SomeTests.FakeService.Run", fake);
        Rel(test, helper, RelationshipType.Calls);
        Rel(test, fakeMethod, RelationshipType.Calls);

        Assert.That(Reported(), Is.EquivalentTo(new[] { "SomeTests" }));
    }

    /// <summary>
    ///     xUnit has no class-level attribute - only the [Fact] methods carry one. The attributed member
    ///     marks the class, so the references of its unattributed members are test references too.
    /// </summary>
    [Test]
    public void Calculate_ClassWithOnlyMethodAttributes_IsATestTypeAsAWhole()
    {
        var fixture = _graph.CreateClass("FactTests", _tests);
        var fact = _graph.CreateMethod("FactTests.ShouldWork", fixture);
        fact.Attributes.Add("Fact");
        var plain = _graph.CreateMethod("FactTests.Verify", fixture);

        var helper = _graph.CreateClass("Helper", _production);
        Rel(plain, helper, RelationshipType.Uses);

        Assert.That(FindingFor(helper).Hints.HasFlag(DeadCodeHint.UsedOnlyByTests), Is.True);
    }

    /// <summary>
    ///     The reason for the type granularity: with the whole assembly classified, one embedded test
    ///     class made every reference leaving the assembly a test reference, and code in other
    ///     assemblies used from ordinary production code was falsely used-only-by-tests.
    /// </summary>
    [Test]
    public void Calculate_EmbeddedTestClass_DoesNotPoisonItsAssembly()
    {
        var caller = _graph.CreateClass("Caller", _production);
        _graph.CreateClass("SmokeTests", _production).Attributes.Add("TestFixture");

        var library = _graph.CreateAssembly("Library");
        var helper = _graph.CreateClass("Helper", library);
        Rel(caller, helper, RelationshipType.Uses);

        Assert.That(Reported(), Does.Not.Contain("Helper"));
    }

    /// <summary>
    ///     The other direction the assembly rule got wrong: production code beside the embedded tests,
    ///     used only by them, was never found - the whole assembly was exempt.
    /// </summary>
    [Test]
    public void Calculate_ProductionCodeOnlyUsedByAnEmbeddedTest_IsReported()
    {
        var service = _graph.CreateClass("Service", _production);
        var smokeTests = _graph.CreateClass("SmokeTests", _production);
        smokeTests.Attributes.Add("TestFixture");
        Rel(smokeTests, service, RelationshipType.Uses);

        Assert.That(FindingFor(service).Hints.HasFlag(DeadCodeHint.UsedOnlyByTests), Is.True);
    }

    /// <summary>
    ///     Matching is case-sensitive: the framework attribute names are exact, and a domain attribute
    ///     that happens to be called "test" must not turn its class into test code.
    /// </summary>
    [Test]
    public void Calculate_LowercaseTestAttribute_DoesNotMarkTestCode()
    {
        var caller = _graph.CreateClass("AbTestRunner", _production);
        caller.Attributes.Add("test");

        var helper = _graph.CreateClass("Helper", _production);
        Rel(caller, helper, RelationshipType.Uses);

        Assert.That(Reported(), Does.Not.Contain("Helper"));
    }

    /// <summary>The test class itself is unreferenced as ever, and keeps saying so.</summary>
    [Test]
    public void Calculate_TestClassItself_IsStillReportedAsTestCode()
    {
        var test = CreateTestClass("SomeTests");

        var finding = FindingFor(test);

        Assert.Multiple(() =>
        {
            Assert.That(finding.Hints.HasFlag(DeadCodeHint.TestCode), Is.True);
            Assert.That(finding.Hints.HasFlag(DeadCodeHint.UsedOnlyByTests), Is.False);
            Assert.That(finding.Confidence, Is.EqualTo(DeadCodeConfidence.Low));
        });
    }

    /// <summary>
    ///     The cap: unreferenced and internal would be the highest confidence, but something demonstrably
    ///     references this one - whether the test alone justifies keeping it is a decision, not a fact.
    /// </summary>
    [Test]
    public void Calculate_InternalClassOnlyUsedByATest_IsCappedAtMedium()
    {
        var helper = _graph.CreateClass("Helper", _production, accessLevel: AccessLevel.Internal);
        Rel(CreateTestClass("HelperTests"), helper, RelationshipType.Uses);

        var unreferenced = _graph.CreateClass("Unused", _production, accessLevel: AccessLevel.Internal);

        Assert.Multiple(() =>
        {
            Assert.That(FindingFor(helper).Confidence, Is.EqualTo(DeadCodeConfidence.Medium));

            // Same visibility, nothing referencing it at all - the level the cap takes away.
            Assert.That(FindingFor(unreferenced).Confidence, Is.EqualTo(DeadCodeConfidence.High));
        });
    }

    /// <summary>
    ///     Liveness travelling from a contract member to its implementations has to keep the colour.
    ///     Production calling the contract keeps the implementation alive.
    /// </summary>
    [Test]
    public void Calculate_ImplementationOfAContractProductionCalls_IsNotReported()
    {
        var contract = _graph.CreateInterface("IService", _production);
        var contractMethod = _graph.CreateMethod("IService.Run", contract);

        var service = _graph.CreateClass("Service", _production);
        var implementation = _graph.CreateMethod("Service.Run", service);
        Rel(implementation, contractMethod, RelationshipType.Implements);

        var caller = _graph.CreateClass("Caller", _production);
        Rel(caller, contractMethod, RelationshipType.Calls);

        Assert.That(Reported(), Does.Not.Contain("Service.Run"));
    }

    /// <summary>
    ///     ...and a test calling the contract does not. The hint is set although no caller can be named:
    ///     liveness arriving through a contract member is not an edge.
    /// </summary>
    [Test]
    public void Calculate_ImplementationOfAContractOnlyTestsCall_IsReported()
    {
        var contract = _graph.CreateInterface("IService", _production);
        var contractMethod = _graph.CreateMethod("IService.Run", contract);

        var service = _graph.CreateClass("Service", _production);
        var implementation = _graph.CreateMethod("Service.Run", service);
        Rel(implementation, contractMethod, RelationshipType.Implements);

        // Production uses the type, so the finding stays on the method instead of being rolled up.
        Rel(_graph.CreateClass("User", _production), service, RelationshipType.Uses);

        Rel(CreateTestClass("ServiceTests"), contractMethod, RelationshipType.Calls);

        var finding = FindingFor(implementation);

        Assert.Multiple(() =>
        {
            Assert.That(finding.Hints.HasFlag(DeadCodeHint.UsedOnlyByTests), Is.True);
            Assert.That(finding.TestReferences, Is.Empty);
            Assert.That(finding.Confidence, Is.EqualTo(DeadCodeConfidence.Medium));
        });
    }

    /// <summary>
    ///     A graph without any test attribute has no test assembly, so nothing changes for it - this is
    ///     what every importer-produced graph looks like.
    /// </summary>
    [Test]
    public void Calculate_GraphWithoutTests_ReportsOnlyTheUnreferencedElements()
    {
        var used = _graph.CreateClass("Used", _production);
        _graph.CreateClass("Unused", _production);
        var caller = _graph.CreateClass("Caller", _production);
        Rel(caller, used, RelationshipType.Uses);

        Assert.That(Reported(), Is.EquivalentTo(new[] { "Unused", "Caller" }));
    }
}
