using CSharpCodeAnalyst.History.Analyzer;
using CSharpCodeAnalyst.History.Metrics;
using CSharpCodeAnalyst.History.Model;

namespace CodeParserTests.UnitTests.History;

[TestFixture]
public class KnowledgeBuilderTests
{
    private static Artifact Artifact(string localPath, string serverPath, int commits)
    {
        return new Artifact { LocalPath = localPath, ServerPath = serverPath, Commits = commits };
    }

    [Test]
    public void Build_CaseInsensitiveDictionaries_MatchDespiteCasingDifferences()
    {
        // The builder relies on the dictionaries being keyed case-insensitively (the pipeline
        // normalizes them - see DictionaryExtension.ToCaseInsensitivePathKeys). Here the artifact
        // paths differ in casing from the dictionary keys; the lookups must still match.
        var summary = new List<Artifact>
        {
            Artifact(@"c:\repo\src\foo.cs", "/Src/Foo.cs", 5),
            Artifact(@"C:\REPO\SRC\BAR.CS", "/Src/Bar.cs", 3)
        };

        var metrics = new Dictionary<string, LinesOfCodeProvider.LinesOfCode>(StringComparer.OrdinalIgnoreCase)
        {
            [@"C:\Repo\Src\Foo.cs"] = new() { Code = 100, Comments = 10 },
            [@"C:\Repo\Src\Bar.cs"] = new() { Code = 50, Comments = 5 }
        };

        var mainDeveloper = new Dictionary<string, MainDeveloper>(StringComparer.OrdinalIgnoreCase)
        {
            [@"C:\Repo\Src\Foo.cs"] = new("Alice", 80.0),
            [@"C:\Repo\Src\Bar.cs"] = new("Bob", 90.0)
        };

        var root = new KnowledgeBuilder().Build(summary, metrics, mainDeveloper);

        var leaves = new List<HotspotNode>();
        root.VisitAll(n =>
        {
            if (n.IsLeafNode)
            {
                leaves.Add(n);
            }
        });

        Assert.That(root.Name, Is.Not.EqualTo("No Data"));
        Assert.That(leaves.Select(l => l.Name), Is.EquivalentTo(new[] { "Foo.cs", "Bar.cs" }));

        // The color key is the main developer, and the raw area is the lines of code.
        var foo = leaves.Single(l => l.Name == "Foo.cs");
        Assert.That(foo.ColorKey, Is.EqualTo("Alice"));
        Assert.That(foo.AreaMetric, Is.EqualTo(100));
    }

    [Test]
    public void Build_FileWithoutContribution_GetsEmptyColorKeyButIsStillIncluded()
    {
        // A file that has a metric but no contribution entry must still appear (with an empty
        // color key that maps to the default brush later), not vanish.
        var summary = new List<Artifact> { Artifact(@"C:\Repo\Src\Foo.cs", "/Src/Foo.cs", 5) };

        var metrics = new Dictionary<string, LinesOfCodeProvider.LinesOfCode>(StringComparer.OrdinalIgnoreCase)
        {
            [@"C:\Repo\Src\Foo.cs"] = new() { Code = 100, Comments = 10 }
        };

        var mainDeveloper = new Dictionary<string, MainDeveloper>(StringComparer.OrdinalIgnoreCase);

        var root = new KnowledgeBuilder().Build(summary, metrics, mainDeveloper);

        var leaves = new List<HotspotNode>();
        root.VisitAll(n =>
        {
            if (n.IsLeafNode)
            {
                leaves.Add(n);
            }
        });

        Assert.That(root.Name, Is.Not.EqualTo("No Data"));
        Assert.That(leaves, Has.Count.EqualTo(1));
        Assert.That(leaves[0].ColorKey, Is.EqualTo(""));
    }
}
