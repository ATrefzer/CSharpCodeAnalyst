using System.Text.Json;
using System.Text.Json.Nodes;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Importers.Dart;
using CSharpCodeAnalyst.Importers.Doxygen;

namespace CodeParserTests.UnitTests.Import;

/// <summary>
///     The half of the Dart fixture test that actually runs the extractor. Explicit because it needs
///     a Dart SDK on the PATH, which the build agent does not have - DartFixtureApprovalTests asserts
///     the mapping from the recorded output instead.
///     Run this after any change to DartExtractor: <see cref="RecordedGraphIsUpToDate" /> tells you
///     whether the output moved, <see cref="ReRecordTheGraph" /> writes the new file for review.
/// </summary>
[TestFixture]
[Explicit("Needs a Dart SDK on the PATH.")]
public class DartFixtureRecordingTests
{
    [SetUp]
    public void SetUp()
    {
        if (DartRunner.FindDartExecutable() is null)
        {
            Assert.Ignore("No Dart SDK found on the PATH.");
        }

        if (!Directory.Exists(DartFixture.PackageDirectory))
        {
            Assert.Ignore($"TestSuiteDart not found at {DartFixture.PackageDirectory}.");
        }

        _workingDirectory = Path.Combine(Path.GetTempPath(), "DartFixtureRecording_" + Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, true);
        }
    }

    private string _workingDirectory = null!;

    /// <summary>
    ///     Source-tree path of the recorded file, not the copy next to the test binary - re-recording
    ///     has to land in the repository to be reviewable.
    /// </summary>
    private static string SourceTreeGraphPath
    {
        get => Path.GetFullPath(Path.Combine(DartFixture.PackageDirectory, "..", "Tests", "TestData", "dart-fixture-graph.json"));
    }

    private async Task<string> RunExtractorAsync()
    {
        // The extractor sources are copied next to the test binary through the Importers reference.
        return await DartRunner.RunAsync(DartFixture.PackageDirectory, _workingDirectory, AppContext.BaseDirectory, null);
    }

    private static (HashSet<string> Elements, HashSet<string> Relationships) Summarize(string jsonPath)
    {
        var graph = new DartGraphConverter().ConvertFile(jsonPath);

        var elements = graph.Nodes.Values
            .Select(e => $"{e.ElementType} {e.FullName} external={e.IsExternal}")
            .ToHashSet();

        var relationships = graph.GetAllRelationships()
            .Select(r => $"{graph.Nodes[r.SourceId].FullName} -{r.Type}-> {graph.Nodes[r.TargetId].FullName}")
            .ToHashSet();

        return (elements, relationships);
    }

    [Test]
    public async Task RecordedGraphIsUpToDate()
    {
        var freshJson = await RunExtractorAsync();

        var recorded = Summarize(DartFixture.RecordedGraphPath);
        var fresh = Summarize(freshJson);

        // Source locations are absolute and therefore machine specific - they are deliberately not
        // part of the comparison.
        Assert.Multiple(() =>
        {
            Assert.That(fresh.Elements, Is.EquivalentTo(recorded.Elements),
                "The extractor produces different elements than TestData/dart-fixture-graph.json. "
                + "Review the change, then run ReRecordTheGraph and update DartFixtureApprovalTests.");
            Assert.That(fresh.Relationships, Is.EquivalentTo(recorded.Relationships),
                "The extractor produces different relationships than TestData/dart-fixture-graph.json. "
                + "Review the change, then run ReRecordTheGraph and update DartFixtureApprovalTests.");
        });
    }

    /// <summary>
    ///     Developer tool: overwrites the recorded file in the source tree. Absolute paths are
    ///     replaced by a stable prefix and the JSON is indented, so the result is reviewable in a diff.
    /// </summary>
    [Test]
    [Explicit("Overwrites Tests/TestData/dart-fixture-graph.json - run deliberately.")]
    public async Task ReRecordTheGraph()
    {
        var freshJson = await RunExtractorAsync();

        var content = await File.ReadAllTextAsync(freshJson);
        var node = JsonNode.Parse(content)!;
        NormalizeLocations(node, DartFixture.PackageDirectory);

        var target = SourceTreeGraphPath;
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await File.WriteAllTextAsync(target, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        TestContext.Out.WriteLine($"Recorded {target}");
    }

    private static void NormalizeLocations(JsonNode root, string packageDirectory)
    {
        var prefix = packageDirectory.TrimEnd(Path.DirectorySeparatorChar);
        foreach (var element in root["elements"]!.AsArray())
        {
            if (element?["location"]?["file"] is not { } file)
            {
                continue;
            }

            var path = file.GetValue<string>();
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                element!["location"]!["file"] = "TestSuiteDart" + path[prefix.Length..];
            }
        }
    }
}
