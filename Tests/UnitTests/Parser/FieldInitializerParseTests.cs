using CSharpCodeAnalyst.CodeGraph.Algorithms.DeadCode;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     A constructor called from a field initializer gets a Calls edge anchored on the field - the same
///     anchoring every method invocation in an initializer already gets. The edge used to be skipped
///     there, which left a constructor only called from field initializers (a common shape for statics
///     like "static readonly List&lt;X&gt; Default = [new(...)]") without a single incoming reference,
///     and the dead code analysis reported it.
/// </summary>
[TestFixture]
public class FieldInitializerParseTests
{
    [OneTimeSetUp]
    public async Task ParseCode()
    {
        const string code = """
                            using System.Collections.Generic;

                            namespace Demo;

                            public class Widget
                            {
                                public Widget(string title) { }
                            }

                            public class Factory
                            {
                                // Target-typed new inside a collection expression.
                                internal static readonly List<Widget> Default = [new("default")];

                                // Explicit new.
                                private Widget _single = new Widget("single");

                                public static List<Widget> All()
                                {
                                    return Default;
                                }

                                public Widget Single()
                                {
                                    return _single;
                                }
                            }
                            """;

        var parser = new CSharpCodeAnalyst.CodeParser.Parser.Parser(
            new ParserConfig(new ProjectExclusionRegExCollection(), false));
        var result = await parser.ParseSourceAsync(code);
        _graph = result.CodeGraph;
    }

    private CodeGraph _graph = null!;

    private CodeElement Element(string name, CodeElementType type)
    {
        return _graph.Nodes.Values.Single(n => n.Name == name && n.ElementType == type);
    }

    private bool HasCallsEdge(CodeElement source, CodeElement target)
    {
        return source.Relationships.Any(r => r.TargetId == target.Id && r.Type == RelationshipType.Calls);
    }

    [Test]
    public void ImplicitNewInACollectionExpressionInitializer_CallsTheConstructor()
    {
        var constructor = Element(".ctor", CodeElementType.Method);
        var field = Element("Default", CodeElementType.Field);

        Assert.That(HasCallsEdge(field, constructor), Is.True);
    }

    [Test]
    public void ExplicitNewInAFieldInitializer_CallsTheConstructor()
    {
        var constructor = Element(".ctor", CodeElementType.Method);
        var field = Element("_single", CodeElementType.Field);

        Assert.That(HasCallsEdge(field, constructor), Is.True);
    }

    [Test]
    public void ConstructorOnlyCalledFromFieldInitializers_IsNotDeadCode()
    {
        var reported = DeadCodeAnalysis.Calculate(_graph).Select(f => f.Element.Name).ToList();

        Assert.That(reported, Does.Not.Contain(".ctor"));
    }
}
