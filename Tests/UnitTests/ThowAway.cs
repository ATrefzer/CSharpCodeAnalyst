using System.Text;
using CSharpCodeAnalyst.CodeParser.Parser;
using CSharpCodeAnalyst.History.Metrics;

namespace CodeParserTests.UnitTests;

//[TestFixture]
public class ThowAway
{
    //[Test]
    public void FileMetrics()
    {
        var provider = new LinesOfCodeProvider(null);

        var path = @"d:\Repositories\CSharpCodeAnalyst";

        // Pure counting
        var result1 = provider.AnalyzeDirectory(path);

        var ordered1 = result1.OrderBy(t => t.Key).ToList();
        var builder = new StringBuilder();
        foreach (var item in ordered1)
        {
            builder.AppendLine($"{item.Key}: Code={item.Value.Code} Comment={item.Value.Comments}");
        }

        File.WriteAllText("d:\\counting.txt", builder.ToString());


        // Roslyn
        provider.RegisterCustomProvider(".cs", SourceMetricsCollector.ComputeForFile);
        var result2 = provider.AnalyzeDirectory(path);

        var ordered2 = result2.OrderBy(tpl => tpl.Key).ToList();
        builder = new StringBuilder();
        foreach (var item in ordered2)
        {
            builder.AppendLine($"{item.Key}: Code={item.Value.Code} Comment={item.Value.Comments}");
        }

        File.WriteAllText("d:\\roslyn.txt", builder.ToString());


        var intersect = ordered1.Intersect(ordered2);

        var d1 = ordered1.Except(ordered2);
        var d2 = ordered2.Except(ordered1);
        var diff = d1.Union(d2);
        builder = new StringBuilder();
        foreach (var item in diff.OrderBy(t => t.Key))
        {
            builder.AppendLine($"{item.Key}: Code={item.Value.Code} Comment={item.Value.Comments}");
        }

        File.WriteAllText("d:\\diff.txt", builder.ToString());
    }
}