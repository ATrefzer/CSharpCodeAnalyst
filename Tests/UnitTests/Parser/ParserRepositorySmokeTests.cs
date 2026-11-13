using CSharpCodeAnalyst.CodeParser.Parser;
using CSharpCodeAnalyst.CodeParser.Parser.Config;
using Microsoft.Build.Locator;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>Opt-in MSBuild imports of real repository projects; kept out of the fast in-memory suite.</summary>
[TestFixture]
[Explicit("Imports real projects through MSBuild; requires a restored repository and installed .NET SDK.")]
public class ParserRepositorySmokeTests
{
    [OneTimeSetUp]
    public void Initialize()
    {
        if (!MSBuildLocator.IsRegistered)
        {
            Initializer.InitializeMsBuildLocator();
        }
    }

    [TestCase("CSharpCodeAnalyst.CodeGraph")]
    [TestCase("CSharpCodeAnalyst.CodeParser")]
    public async Task RepositoryProject_HasResolvableDependenciesAndValidGraph(string projectName)
    {
        var root = FindRepositoryRoot();
        var parser = new CSharpCodeAnalyst.CodeParser.Parser.Parser(
            new ParserConfig(new ProjectExclusionRegExCollection(), includeExternals: false, includeXamlReferences: false));
        var result = await parser.ParseAsync(Path.Combine(root, projectName, projectName + ".csproj"));
        Assert.That(parser.Diagnostics.Failures, Is.Empty, parser.Diagnostics.FormatFailures());
        Assert.That(result.CodeGraph.Nodes.Count, Is.GreaterThan(100));
        GraphInvariantAssertions.AssertValid(result.CodeGraph);
        TestContext.WriteLine($"{projectName}: {result.CodeGraph.Nodes.Count} nodes, " +
                              $"{result.CodeGraph.Nodes.Values.Sum(n => n.Relationships.Count)} relationships");
    }

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { TestContext.CurrentContext.WorkDirectory, TestContext.CurrentContext.TestDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "CSharpCodeAnalyst.CodeParser", "CSharpCodeAnalyst.CodeParser.csproj")))
                {
                    return directory.FullName;
                }
            }
        }

        throw new DirectoryNotFoundException("Run from the repository root, or set NUnit's work directory to that root.");
    }
}
