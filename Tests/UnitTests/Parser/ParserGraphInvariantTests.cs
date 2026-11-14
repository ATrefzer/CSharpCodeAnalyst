using CSharpCodeAnalyst.CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

[TestFixture]
public class ParserGraphInvariantTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task MixedDeclarationsAndTopLevelCode_KeepGraphStructurallyValid(bool includeExternals)
    {
        var parser = new CSharpCodeAnalyst.CodeParser.Parser.Parser(
            new ParserConfig(new ProjectExclusionRegExCollection(), includeExternals: includeExternals, includeXamlReferences: false));
        var result = await parser.ParseSourceAsync("""
            System.Console.WriteLine(new Demo.Item(1).Value);
            namespace Demo
            {
                class MarkAttribute : System.Attribute { public MarkAttribute(System.Type type) { } }
                interface IContract { int Value { get; } }
                record Item(int Value) : IContract;
                class Outer<T>
                {
                    public class Inner
                    {
                        public event System.Action Changed = Handler;
                        static void Handler() { }
                        [return: Mark(typeof(Item))]
                        Item Create([Mark(typeof(IContract))] int value)
                        {
                            Item Local(Item item) => item;
                            return Local(new Item(value));
                        }
                    }
                }
            }
            """);
        Assert.That(parser.Diagnostics.Failures, Is.Empty, parser.Diagnostics.FormatFailures());
        GraphInvariantAssertions.AssertValid(result.CodeGraph);
        Assert.That(result.CodeGraph.Nodes.Values.Any(n => n.IsExternal), Is.EqualTo(includeExternals));
    }
}
