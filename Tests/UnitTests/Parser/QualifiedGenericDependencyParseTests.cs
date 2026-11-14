using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

[TestFixture]
public class QualifiedGenericDependencyParseTests : InMemoryParseTestBase
{
    protected override string Code => "";

    [TestCase("Registry<Token>.Instance", false)]
    [TestCase("Demo.Registry<Token>.Instance", false)]
    [TestCase("global::Demo.Registry<Token>.Instance", false)]
    [TestCase("Alias.Instance", false)]
    [TestCase("Demo.Registry<Token>.Instance", true)]
    [TestCase("global::Demo.Registry<Token>.Instance", true)]
    [TestCase("Alias.Instance", true)]
    [TestCase("Outer<Token>.Inner<Other>.Instance", false)]
    [TestCase("Demo.Outer<Token>.Inner<Other>.Instance", false)]
    [TestCase("NestedAlias.Instance", false)]
    public async Task TypeExpression_PreservesArguments(string expression, bool inLambda)
    {
        var statement = inLambda ? $"System.Func<int> read = () => {expression};" : $"_ = {expression};";
        var code = $$"""
            using Alias = Demo.Registry<Demo.Token>;
            using NestedAlias = Demo.Outer<Demo.Token>.Inner<Demo.Other>;
            namespace Demo;
            class Token { }
            class Other { }
            class Registry<T> { public static int Instance; }
            class Outer<T> { public class Inner<U> { public static int Instance; } }
            class Caller { void Read() { {{statement}} } }
            """;
        var parser = new CSharpCodeAnalyst.CodeParser.Parser.Parser(new ParserConfig(new ProjectExclusionRegExCollection(), false, false));
        Graph = (await parser.ParseSourceAsync(code)).CodeGraph;
        Assert.That(parser.Diagnostics.Failures, Is.Empty, parser.Diagnostics.FormatFailures());
        var dependencies = RelsOf(RelationshipType.Uses);
        Assert.That(dependencies, Does.Contain("Caller.Read -> Token"));
        if (expression.Contains("Inner") || expression.Contains("NestedAlias"))
        {
            Assert.That(dependencies, Does.Contain("Caller.Read -> Other"));
        }
    }

    [TestCase("Outer<Token>.Inner")]
    [TestCase("Outer<Token>.Generic<Other>")]
    [TestCase("Outer<Token[]>.Generic<Other[]>")]
    [TestCase("System.Collections.Generic.List<Outer<Token>.Generic<Other>>")]
    public async Task NestedTypeSignature_PreservesContainingArguments(string type)
    {
        var code = $$"""
            class Token { }
            class Other { }
            class Outer<T> { public class Inner { } public class Generic<U> { } }
            class Caller { {{type}} Value; }
            """;
        var parser = new CSharpCodeAnalyst.CodeParser.Parser.Parser(new ParserConfig(new ProjectExclusionRegExCollection(), false, false));
        Graph = (await parser.ParseSourceAsync(code)).CodeGraph;
        Assert.That(parser.Diagnostics.Failures, Is.Empty, parser.Diagnostics.FormatFailures());
        var dependencies = RelsOf(RelationshipType.Uses);
        Assert.That(dependencies, Does.Contain("Caller.Value -> Token"));
        if (type.Contains("Other"))
        {
            Assert.That(dependencies, Does.Contain("Caller.Value -> Other"));
        }
    }
}
