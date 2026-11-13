using CSharpCodeAnalyst.CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

[TestFixture]
public class CompilationDiagnosticsParseTests
{
    [Test]
    public async Task UnresolvedCall_ReportsCompilerErrorButKeepsResolvableRelationships()
    {
        var parser = CreateParser();
        var result = await parser.ParseSourceAsync("""
                                                  class Helper { public static void Run() { } }
                                                  class Caller
                                                  {
                                                      void M()
                                                      {
                                                          Helper.Run();
                                                          MissingLibrary.DoWork();
                                                      }
                                                  }
                                                  """, "Broken.cs");
        var method = result.CodeGraph.Nodes.Values.Single(n => n.Name == "M");
        var helper = result.CodeGraph.Nodes.Values.Single(n => n.Name == "Run");

        Assert.Multiple(() =>
        {
            Assert.That(parser.Diagnostics.HasDiagnostics, Is.True);
            Assert.That(parser.Diagnostics.Failures.Any(message => message.Contains("CS0103") &&
                message.Contains("Broken.cs") && message.Contains("InMemory")), Is.True);
            Assert.That(method.Relationships.Any(r => r.TargetId == helper.Id), Is.True);
        });
    }

    [Test]
    public async Task SyntaxErrors_AreReported()
    {
        var parser = CreateParser();
        await parser.ParseSourceAsync("class Broken {", "SyntaxError.cs");

        Assert.That(parser.Diagnostics.Failures.Any(message => message.Contains("CS1513") && message.Contains("SyntaxError.cs")), Is.True);
    }

    [Test]
    public async Task NextParse_ClearsPreviousCompilationFailures()
    {
        var parser = CreateParser();
        await parser.ParseSourceAsync("class Broken { MissingType field; }");
        Assert.That(parser.Diagnostics.Failures, Is.Not.Empty);

        await parser.ParseSourceAsync("class Valid { }");
        Assert.That(parser.Diagnostics.HasDiagnostics, Is.False);
    }

    [TestCase("class Library { }")]
    [TestCase("System.Console.WriteLine(1);")]
    [TestCase("class WarningsOnly { private int unused; }")]
    public async Task ValidSource_DoesNotReportSyntheticEntryPointErrorsOrOrdinaryWarnings(string code)
    {
        var parser = CreateParser();
        await parser.ParseSourceAsync(code);

        Assert.That(parser.Diagnostics.HasDiagnostics, Is.False, parser.Diagnostics.FormatFailures());
    }

    private static CSharpCodeAnalyst.CodeParser.Parser.Parser CreateParser()
    {
        return new CSharpCodeAnalyst.CodeParser.Parser.Parser(
            new ParserConfig(new ProjectExclusionRegExCollection(), false, false));
    }
}
