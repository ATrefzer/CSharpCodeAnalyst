using System;
using System.IO;
using CodeGraph.Graph;
using CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     ParseAsync also accepts a single ".cs" file (reachable from the import dialog by typing a .cs name
///     into the file mask). It reads the text from disk and runs it through the in-memory pipeline.
/// </summary>
[TestFixture]
public class SingleFileParseTests
{
    [Test]
    public void ParseAsync_OnCsFile_ReadsFromDiskAndKeepsRealSourceLocation()
    {
        var path = Path.Combine(Path.GetTempPath(), "CSharpCodeAnalyst_" + Guid.NewGuid().ToString("N") + ".cs");
        File.WriteAllText(path, """
                                namespace Demo;

                                public class Foo
                                {
                                    public void Bar() { }
                                }
                                """);

        try
        {
            var parser = new CodeParser.Parser.Parser(new ParserConfig(new ProjectExclusionRegExCollection(), false));
            var graph = parser.ParseAsync(path).GetAwaiter().GetResult();

            var foo = graph.Nodes.Values.SingleOrDefault(n => n.ElementType == CodeElementType.Class && n.Name == "Foo");
            var bar = graph.Nodes.Values.SingleOrDefault(n => n.ElementType == CodeElementType.Method && n.Name == "Bar");

            Assert.Multiple(() =>
            {
                Assert.That(foo, Is.Not.Null, "The class from the parsed file should be in the graph.");
                Assert.That(bar, Is.Not.Null, "The method from the parsed file should be in the graph.");
                // The source location points at the real file on disk, so "Jump to Code" works.
                Assert.That(bar!.SourceLocations.Any(l => l.File == path), Is.True);
            });
        }
        finally
        {
            File.Delete(path);
        }
    }
}
