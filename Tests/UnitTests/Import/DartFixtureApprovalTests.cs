using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Importers.Dart;
using CSharpCodeAnalyst.Importers.Doxygen;

namespace CodeParserTests.UnitTests.Import;

/// <summary>
///     Pins the Dart element and relationship mapping against the handcrafted TestSuiteDart package.
///     Runs from the recorded extractor output in TestData, so it needs no Dart SDK and runs on the
///     build agent; DartFixtureRecordingTests re-records that file and is the half that needs a SDK.
///     When the extractor legitimately changes: run the recording test, review the diff of
///     dart-fixture-graph.json, and update the expectations below.
/// </summary>
[TestFixture]
public class DartFixtureApprovalTests
{
    [OneTimeSetUp]
    public void SetUp()
    {
        var converter = new DartGraphConverter();
        _graph = converter.ConvertFile(DartFixture.RecordedGraphPath);
        _converter = converter;
    }

    private CSharpCodeAnalyst.CodeGraph.Graph.CodeGraph _graph = null!;
    private DartGraphConverter _converter = null!;

    private IEnumerable<CodeElement> ProjectElements => _graph.Nodes.Values.Where(e => !e.IsExternal);

    private CodeElement ByFullName(string fullName)
    {
        return _graph.Nodes.Values.Single(e => e.FullName == fullName);
    }

    private HashSet<(string, RelationshipType, string)> RelationshipsOf(string sourceFullName)
    {
        var source = ByFullName(sourceFullName);
        return source.Relationships
            .Select(r => (_graph.Nodes[r.SourceId].FullName, r.Type, _graph.Nodes[r.TargetId].FullName))
            .ToHashSet();
    }

    [Test]
    public void ConvertsWithoutLosingAnything()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_converter.SkippedElements, Is.Zero);
            Assert.That(_converter.SkippedRelationships, Is.Zero);
            Assert.That(_graph.Nodes.Values.Where(e => e.Parent is null).All(e => e.ElementType == CodeElementType.Assembly), Is.True);
        });
    }

    /// <summary>
    ///     The complete project-side element set. Catches any unintended drift; the tests below
    ///     spell out the rules that are easy to get wrong.
    /// </summary>
    [Test]
    public void MapsEveryElement()
    {
        var actual = ProjectElements.Select(e => (e.FullName, e.ElementType)).ToHashSet();

        Assert.That(actual, Is.EquivalentTo(new[]
        {
            ("test_suite_dart", CodeElementType.Assembly),

            ("test_suite_dart.types", CodeElementType.Namespace),
            ("test_suite_dart.types.PlainClass", CodeElementType.Class),
            ("test_suite_dart.types.PlainClass.value", CodeElementType.Field),
            ("test_suite_dart.types.PlainClass.new", CodeElementType.Method),
            ("test_suite_dart.types.AbstractBase", CodeElementType.Class),
            ("test_suite_dart.types.AbstractBase.template", CodeElementType.Method),
            ("test_suite_dart.types.AbstractBase.shared", CodeElementType.Method),
            ("test_suite_dart.types.AbstractBase.new", CodeElementType.Method),
            ("test_suite_dart.types.PureInterface", CodeElementType.Interface),
            ("test_suite_dart.types.PureInterface.contract", CodeElementType.Method),
            ("test_suite_dart.types.PureInterface.new", CodeElementType.Method),
            ("test_suite_dart.types.Named", CodeElementType.Interface),
            ("test_suite_dart.types.Named.name", CodeElementType.Property),
            ("test_suite_dart.types.Named.new", CodeElementType.Method),
            ("test_suite_dart.types.Greeting", CodeElementType.Class),
            ("test_suite_dart.types.Greeting.greet", CodeElementType.Method),
            ("test_suite_dart.types.CountingLog", CodeElementType.Class),
            ("test_suite_dart.types.CountingLog.count", CodeElementType.Field),
            ("test_suite_dart.types.CountingLog.log", CodeElementType.Method),
            ("test_suite_dart.types.Combined", CodeElementType.Class),
            ("test_suite_dart.types.Combined.name", CodeElementType.Property),
            ("test_suite_dart.types.Combined.template", CodeElementType.Method),
            ("test_suite_dart.types.Combined.new", CodeElementType.Method),
            ("test_suite_dart.types.Color", CodeElementType.Enum),
            ("test_suite_dart.types.Color.red", CodeElementType.Field),
            ("test_suite_dart.types.Color.green", CodeElementType.Field),
            ("test_suite_dart.types.Color.blue", CodeElementType.Field),
            ("test_suite_dart.types.Color.values", CodeElementType.Field),
            ("test_suite_dart.types.Color.isWarm", CodeElementType.Property),
            ("test_suite_dart.types.Color.new", CodeElementType.Method),
            ("test_suite_dart.types.StringPadding", CodeElementType.Class),
            ("test_suite_dart.types.StringPadding.padBoth", CodeElementType.Method),
            ("test_suite_dart.types.Meters", CodeElementType.Struct),
            ("test_suite_dart.types.Meters.value", CodeElementType.Field),
            ("test_suite_dart.types.Meters.inCentimeters", CodeElementType.Property),
            ("test_suite_dart.types.Meters.new", CodeElementType.Method),
            ("test_suite_dart.types.ColorPicker", CodeElementType.Delegate),
            ("test_suite_dart.types.ColorList", CodeElementType.Delegate),
            ("test_suite_dart.types.doubleIt", CodeElementType.Method),

            ("test_suite_dart.members", CodeElementType.Namespace),
            ("test_suite_dart.members.pickFirst", CodeElementType.Method),
            ("test_suite_dart.members.defaultWidth", CodeElementType.Field),
            ("test_suite_dart.members.Account", CodeElementType.Class),
            ("test_suite_dart.members.Account.new", CodeElementType.Method),
            ("test_suite_dart.members.Account.empty", CodeElementType.Method),
            ("test_suite_dart.members.Account.copy", CodeElementType.Method),
            ("test_suite_dart.members.Account.instances", CodeElementType.Field),
            ("test_suite_dart.members.Account._balance", CodeElementType.Field),
            ("test_suite_dart.members.Account.balance", CodeElementType.Property),
            ("test_suite_dart.members.Account.isEmpty", CodeElementType.Property),
            ("test_suite_dart.members.Account.+", CodeElementType.Method),
            ("test_suite_dart.members.Ledger", CodeElementType.Class),
            ("test_suite_dart.members.Ledger.accounts", CodeElementType.Field),
            ("test_suite_dart.members.Ledger.new", CodeElementType.Method),

            ("test_suite_dart.library_with_part", CodeElementType.Namespace),
            ("test_suite_dart.library_with_part.Bookkeeper", CodeElementType.Class),
            ("test_suite_dart.library_with_part.Bookkeeper.ledger", CodeElementType.Field),
            ("test_suite_dart.library_with_part.Bookkeeper.total", CodeElementType.Method),
            ("test_suite_dart.library_with_part.Bookkeeper.new", CodeElementType.Method),
            ("test_suite_dart.library_with_part._sumBalances", CodeElementType.Method),
            ("test_suite_dart.library_with_part.PartLocalHelper", CodeElementType.Class),
            ("test_suite_dart.library_with_part.PartLocalHelper.new", CodeElementType.Method),

            ("test_suite_dart.features", CodeElementType.Namespace),
            ("test_suite_dart.features.reporting", CodeElementType.Namespace),
            ("test_suite_dart.features.reporting.report_builder", CodeElementType.Namespace),
            ("test_suite_dart.features.reporting.report_builder.Important", CodeElementType.Class),
            ("test_suite_dart.features.reporting.report_builder.Important.new", CodeElementType.Method),
            ("test_suite_dart.features.reporting.report_builder.ReportBuilder", CodeElementType.Class),
            ("test_suite_dart.features.reporting.report_builder.ReportBuilder.new", CodeElementType.Method),
            ("test_suite_dart.features.reporting.report_builder.ReportBuilder.accounts", CodeElementType.Field),
            ("test_suite_dart.features.reporting.report_builder.ReportBuilder.template", CodeElementType.Method),
            ("test_suite_dart.features.reporting.report_builder.ReportBuilder.merge", CodeElementType.Method),
            ("test_suite_dart.features.reporting.report_builder.ReportBuilder.balancesDeferred", CodeElementType.Method),
            ("test_suite_dart.features.reporting.report_builder.ReportBuilder.picker", CodeElementType.Property),
            ("test_suite_dart.features.reporting.report_builder.ReportBuilder._pickColor", CodeElementType.Method),
            ("test_suite_dart.features.reporting.report_builder.ReportBuilder.describe", CodeElementType.Method)
        }));
    }

    [Test]
    public void DropsUnnamedExtensionsEntirely()
    {
        Assert.Multiple(() =>
        {
            // "extension on int { int get doubled }" has no name to reference it by.
            Assert.That(_graph.Nodes.Values.Any(e => e.Name == "doubled"), Is.False);

            // doubleIt() reads that member; the reference must vanish rather than resurrect an
            // anonymous node. Its signature types (int) legitimately remain.
            Assert.That(RelationshipsOf("test_suite_dart.types.doubleIt"), Is.EquivalentTo(new[]
            {
                ("test_suite_dart.types.doubleIt", RelationshipType.Uses, "dart:core.global.int")
            }));
        });
    }

    [Test]
    public void FoldsPartFilesIntoTheirLibrary()
    {
        Assert.Multiple(() =>
        {
            // Declared in lib/parts/ledger_part.dart, but part of lib/library_with_part.dart.
            Assert.That(ByFullName("test_suite_dart.library_with_part._sumBalances").Parent!.Name, Is.EqualTo("library_with_part"));
            Assert.That(ByFullName("test_suite_dart.library_with_part.PartLocalHelper").Parent!.Name, Is.EqualTo("library_with_part"));

            // The directory of the part must not become a namespace of its own.
            Assert.That(ProjectElements.Any(e => e.Name is "parts" or "ledger_part"), Is.False);
        });
    }

    /// <summary>
    ///     A hand-written accessor and a field induce each other as synthetic counterparts. Which of
    ///     the two owns the element is a modelling decision, not an accident of iteration order.
    /// </summary>
    [Test]
    public void LetsAccessorsWinOverTheirSyntheticField()
    {
        Assert.Multiple(() =>
        {
            // Getter and setter of "balance" collapse into a single Property.
            Assert.That(ProjectElements.Count(e => e.FullName == "test_suite_dart.members.Account.balance"), Is.EqualTo(1));
            Assert.That(ByFullName("test_suite_dart.members.Account.balance").ElementType, Is.EqualTo(CodeElementType.Property));

            // The backing field is declared by hand and stays a field.
            Assert.That(ByFullName("test_suite_dart.members.Account._balance").ElementType, Is.EqualTo(CodeElementType.Field));

            // An enum's "values" is synthetic through and through - there is no declaration to
            // prefer over it, so it stays a field.
            Assert.That(ByFullName("test_suite_dart.types.Color.values").ElementType, Is.EqualTo(CodeElementType.Field));
        });
    }

    [Test]
    public void MapsTheThreeSupertypeClauses()
    {
        // class Combined extends AbstractBase with Greeting implements Named
        Assert.That(RelationshipsOf("test_suite_dart.types.Combined"), Is.EquivalentTo(new[]
        {
            ("test_suite_dart.types.Combined", RelationshipType.Inherits, "test_suite_dart.types.AbstractBase"),
            ("test_suite_dart.types.Combined", RelationshipType.Inherits, "test_suite_dart.types.Greeting"),
            ("test_suite_dart.types.Combined", RelationshipType.Implements, "test_suite_dart.types.Named")
        }));
    }

    [Test]
    public void ModelsAMixinConstraintAsUses()
    {
        // "mixin CountingLog on AbstractBase" constrains the user of the mixin - that is not
        // inheritance.
        Assert.That(RelationshipsOf("test_suite_dart.types.CountingLog"), Is.EquivalentTo(new[]
        {
            ("test_suite_dart.types.CountingLog", RelationshipType.Uses, "test_suite_dart.types.AbstractBase")
        }));
    }

    [Test]
    public void DetectsOverridesOfMethodsAndGetters()
    {
        var overrides = _graph.GetAllRelationships()
            .Where(r => r.Type == RelationshipType.Overrides && !_graph.Nodes[r.SourceId].IsExternal)
            .Select(r => (_graph.Nodes[r.SourceId].FullName, _graph.Nodes[r.TargetId].FullName))
            .ToHashSet();

        Assert.That(overrides, Is.EquivalentTo(new[]
        {
            ("test_suite_dart.types.Combined.template", "test_suite_dart.types.AbstractBase.template"),
            // Implementing an abstract getter of an interface is an override, too.
            ("test_suite_dart.types.Combined.name", "test_suite_dart.types.Named.name"),
            ("test_suite_dart.features.reporting.report_builder.ReportBuilder.template", "test_suite_dart.types.AbstractBase.template")
        }));
    }

    /// <summary>
    ///     The same property access is a call in a method body and a use inside a closure - a closure
    ///     body is not executed where it is written.
    /// </summary>
    [Test]
    public void DowngradesCallsInsideClosuresToUses()
    {
        var direct = RelationshipsOf("test_suite_dart.library_with_part._sumBalances");
        var inClosure = RelationshipsOf("test_suite_dart.features.reporting.report_builder.ReportBuilder.balancesDeferred");

        Assert.Multiple(() =>
        {
            Assert.That(direct, Does.Contain(
                ("test_suite_dart.library_with_part._sumBalances", RelationshipType.Calls, "test_suite_dart.members.Account.balance")));

            Assert.That(inClosure, Does.Contain(
                ("test_suite_dart.features.reporting.report_builder.ReportBuilder.balancesDeferred", RelationshipType.Uses,
                    "test_suite_dart.members.Account.balance")));
            Assert.That(inClosure.Any(r => r.Item2 == RelationshipType.Calls && r.Item3.EndsWith("Account.balance")), Is.False);
        });
    }

    [Test]
    public void ModelsConstructorInvocationsAndTearOffs()
    {
        Assert.Multiple(() =>
        {
            // The edge points at the constructor, which lives below the created type.
            Assert.That(RelationshipsOf("test_suite_dart.members.Account.copy"), Does.Contain(
                ("test_suite_dart.members.Account.copy", RelationshipType.Creates, "test_suite_dart.members.Account.new")));

            // A named constructor is reached by its own name.
            Assert.That(RelationshipsOf("test_suite_dart.features.reporting.report_builder.ReportBuilder.merge"), Does.Contain(
                ("test_suite_dart.features.reporting.report_builder.ReportBuilder.merge", RelationshipType.Creates,
                    "test_suite_dart.members.Account.empty")));

            // "ColorPicker get picker => _pickColor" references the method without calling it.
            Assert.That(RelationshipsOf("test_suite_dart.features.reporting.report_builder.ReportBuilder.picker"), Does.Contain(
                ("test_suite_dart.features.reporting.report_builder.ReportBuilder.picker", RelationshipType.Uses,
                    "test_suite_dart.features.reporting.report_builder.ReportBuilder._pickColor")));
        });
    }

    [Test]
    public void ReachesTypeArgumentsAndAliasedTypes()
    {
        Assert.Multiple(() =>
        {
            // List<Account> must reach Account, not only List.
            Assert.That(RelationshipsOf("test_suite_dart.members.Ledger.accounts"), Does.Contain(
                ("test_suite_dart.members.Ledger.accounts", RelationshipType.Uses, "test_suite_dart.members.Account")));

            // typedef ColorPicker = Color Function(int) / typedef ColorList = List<Color>
            Assert.That(RelationshipsOf("test_suite_dart.types.ColorPicker"), Does.Contain(
                ("test_suite_dart.types.ColorPicker", RelationshipType.Uses, "test_suite_dart.types.Color")));
            Assert.That(RelationshipsOf("test_suite_dart.types.ColorList"), Does.Contain(
                ("test_suite_dart.types.ColorList", RelationshipType.Uses, "test_suite_dart.types.Color")));
        });
    }

    [Test]
    public void RecordsAnnotationsAsUsesAttribute()
    {
        Assert.That(RelationshipsOf("test_suite_dart.features.reporting.report_builder.ReportBuilder"), Does.Contain(
            ("test_suite_dart.features.reporting.report_builder.ReportBuilder", RelationshipType.UsesAttribute,
                "test_suite_dart.features.reporting.report_builder.Important")));
    }

    [Test]
    public void CollectsMetricsOnlyForMembersWithABody()
    {
        var shared = _converter.Metrics.TryGet(ByFullName("test_suite_dart.types.AbstractBase.shared").Id);
        var abstractMember = _converter.Metrics.TryGet(ByFullName("test_suite_dart.types.AbstractBase.template").Id);

        Assert.Multiple(() =>
        {
            Assert.That(shared, Is.Not.Null);
            Assert.That(shared!.CodeLines, Is.GreaterThan(0));
            Assert.That(shared.CyclomaticComplexity, Is.EqualTo(1));

            // "void template();" has no implementation to measure.
            Assert.That(abstractMember, Is.Null);

            // _sumBalances has a for loop.
            var sumBalances = _converter.Metrics.TryGet(ByFullName("test_suite_dart.library_with_part._sumBalances").Id);
            Assert.That(sumBalances!.CyclomaticComplexity, Is.EqualTo(2));
            Assert.That(sumBalances.LogicalLinesOfCode, Is.EqualTo(4));
        });
    }
}
