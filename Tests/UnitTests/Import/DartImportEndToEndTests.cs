using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Features.Import;

namespace CodeParserTests.UnitTests.Import;

/// <summary>
///     Runs the whole Dart import - tool deployment, the extractor itself, and the conversion -
///     against a real project. Explicit because it needs a Dart or Flutter SDK on the PATH and a
///     project that has been resolved with "pub get"; neither is available on the build agent.
///     Point it at a project with the CSCA_DART_TEST_PROJECT environment variable:
///     $env:CSCA_DART_TEST_PROJECT = "C:\path\to\flutter_app"
/// </summary>
[TestFixture]
[Explicit("Needs a Dart SDK and a resolved project - see CSCA_DART_TEST_PROJECT.")]
public class DartImportEndToEndTests
{
    [SetUp]
    public void SetUp()
    {
        _projectDirectory = Environment.GetEnvironmentVariable("CSCA_DART_TEST_PROJECT");
        if (string.IsNullOrEmpty(_projectDirectory) || !Directory.Exists(_projectDirectory))
        {
            Assert.Ignore("CSCA_DART_TEST_PROJECT is not set to an existing directory.");
        }

        _workingDirectory = Path.Combine(Path.GetTempPath(), "DartImportEndToEndTests_" + Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, true);
        }
    }

    private string? _projectDirectory;
    private string _workingDirectory = null!;

    [Test]
    public void FindsADartSdk()
    {
        Assert.That(DartRunner.FindDartExecutable(), Is.Not.Null,
            "No dart.exe found - install the Dart SDK or Flutter and put it on the PATH.");
    }

    [Test]
    public void RecognizesAResolvedProject()
    {
        Assert.That(DartRunner.IsProjectResolved(_projectDirectory!), Is.True,
            "Run \"flutter pub get\" (or \"dart pub get\") in the test project first.");
    }

    [Test]
    public async Task ProducesAConnectedGraph()
    {
        var jsonPath = await DartRunner.RunAsync(_projectDirectory!, _workingDirectory, null);

        var converter = new DartGraphConverter();
        var graph = converter.ConvertFile(jsonPath);

        Assert.Multiple(() =>
        {
            Assert.That(converter.SkippedElements, Is.Zero, "The extractor emitted element types the converter does not know.");
            Assert.That(converter.SkippedRelationships, Is.Zero, "The extractor emitted relationship types the converter does not know.");

            var internalElements = graph.Nodes.Values.Where(e => !e.IsExternal).ToList();
            Assert.That(internalElements, Is.Not.Empty, "No project code was found - is the project resolved?");
            Assert.That(internalElements.Any(e => e.ElementType == CodeElementType.Assembly), Is.True);
            Assert.That(internalElements.Any(e => e.ElementType == CodeElementType.Class), Is.True);

            // Every element except the assemblies must hang below a parent, and every relationship
            // must connect two known nodes - that is what the rest of the application relies on.
            Assert.That(graph.Nodes.Values.Where(e => e.Parent is null).All(e => e.ElementType == CodeElementType.Assembly), Is.True);
            Assert.That(graph.GetAllRelationships().All(r => graph.Nodes.ContainsKey(r.SourceId) && graph.Nodes.ContainsKey(r.TargetId)), Is.True);
        });
    }
}
