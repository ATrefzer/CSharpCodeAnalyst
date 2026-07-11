using CSharpCodeAnalyst.CodeParser.Parser;

namespace CodeParserTests.UnitTests.Parser;

[TestFixture]
public class ProjectSelectorTests
{
    [Test]
    public void NoDuplicates_KeepsAll_NoDiagnostics()
    {
        var candidates = new List<ProjectCandidate>
        {
            new("Alpha", @"C:\src\Alpha\Alpha.csproj", "Alpha"),
            new("Beta", @"C:\src\Beta\Beta.csproj", "Beta")
        };

        var result = ProjectSelector.SelectProjectsPerAssembly(candidates);

        Assert.Multiple(() =>
        {
            Assert.That(result.Selected.Select(c => c.AssemblyName), Is.EquivalentTo(new[] { "Alpha", "Beta" }));
            Assert.That(result.Warnings, Is.Empty);
            Assert.That(result.Failures, Is.Empty);
        });
    }

    [Test]
    public void MultiTargeting_KeepsHighestTfm_WarnsOnce_NoFailure()
    {
        // Same .csproj opened once per target framework: same file path, TFM in the name.
        var candidates = new List<ProjectCandidate>
        {
            new("Lib", @"C:\src\Lib\Lib.csproj", "Lib (net8.0)"),
            new("Lib", @"C:\src\Lib\Lib.csproj", "Lib (net10.0)")
        };

        var result = ProjectSelector.SelectProjectsPerAssembly(candidates);

        Assert.Multiple(() =>
        {
            Assert.That(result.Selected, Has.Count.EqualTo(1));
            Assert.That(result.Selected[0].ProjectName, Is.EqualTo("Lib (net10.0)"));
            Assert.That(result.Warnings, Has.Count.EqualTo(1));
            Assert.That(result.Warnings[0], Does.Contain("multi-targeted").And.Contain("net10.0"));
            Assert.That(result.Failures, Is.Empty);
        });
    }

    [Test]
    public void RealCollision_DifferentPaths_KeepsOne_ReportsFailure()
    {
        // Two different projects that happen to produce the same assembly name.
        var candidates = new List<ProjectCandidate>
        {
            new("Shared", @"C:\src\A\Shared.csproj", "A.Shared"),
            new("Shared", @"C:\src\B\Shared.csproj", "B.Shared")
        };

        var result = ProjectSelector.SelectProjectsPerAssembly(candidates);

        Assert.Multiple(() =>
        {
            Assert.That(result.Selected, Has.Count.EqualTo(1));
            Assert.That(result.Selected[0].AssemblyName, Is.EqualTo("Shared"));
            Assert.That(result.Failures, Has.Count.EqualTo(1));
            Assert.That(result.Failures[0], Does.Contain("Shared").And.Contain("ignored"));
            Assert.That(result.Warnings, Is.Empty);
        });
    }

    [Test]
    public void Tfm_Ranking_PrefersNewerAndModernFamilies()
    {
        // net10.0 > net8.0 > netcoreapp3.1 > netstandard2.0 / net48.
        AssertChosenTfm(new[] { "net8.0", "net10.0" }, "net10.0");
        AssertChosenTfm(new[] { "net10.0", "net8.0" }, "net10.0");
        AssertChosenTfm(new[] { "netstandard2.0", "net8.0" }, "net8.0");
        AssertChosenTfm(new[] { "netcoreapp3.1", "netstandard2.0" }, "netcoreapp3.1");
        AssertChosenTfm(new[] { "net48", "netstandard2.0" }, "netstandard2.0");

        // net472 is 4.7.2, not 4.72 - net48 must win.
        AssertChosenTfm(new[] { "net472", "net48" }, "net48");
    }

    [Test]
    public void UnparsableTfm_FallsBackToStableNameOrder()
    {
        // Neither name carries a recognizable TFM -> deterministic ordinal tie-break on the name.
        var candidates = new List<ProjectCandidate>
        {
            new("Lib", @"C:\src\Lib\Lib.csproj", "Lib (zzz)"),
            new("Lib", @"C:\src\Lib\Lib.csproj", "Lib (aaa)")
        };

        var result = ProjectSelector.SelectProjectsPerAssembly(candidates);

        Assert.That(result.Selected[0].ProjectName, Is.EqualTo("Lib (aaa)"));
    }

    private static void AssertChosenTfm(string[] tfms, string expected)
    {
        var candidates = tfms
            .Select(tfm => new ProjectCandidate("Lib", @"C:\src\Lib\Lib.csproj", $"Lib ({tfm})"))
            .ToList();

        var result = ProjectSelector.SelectProjectsPerAssembly(candidates);

        Assert.That(result.Selected, Has.Count.EqualTo(1));
        Assert.That(result.Selected[0].ProjectName, Is.EqualTo($"Lib ({expected})"),
            $"Expected '{expected}' to win among [{string.Join(", ", tfms)}]");
    }
}
