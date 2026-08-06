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

                                // Property initializers share the anchoring.
                                public Widget Prop { get; } = new("prop");

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

    private bool HasEdge(CodeElement source, CodeElement target, RelationshipType type)
    {
        return source.Relationships.Any(r => r.TargetId == target.Id && r.Type == type);
    }

    private bool HasCallsEdge(CodeElement source, CodeElement target)
    {
        return HasEdge(source, target, RelationshipType.Calls);
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
    public void PropertyInitializer_CallsTheConstructor()
    {
        var constructor = Element(".ctor", CodeElementType.Method);
        var property = Element("Prop", CodeElementType.Property);

        Assert.That(HasCallsEdge(property, constructor), Is.True);
    }

    [Test]
    public void CreatesIsAnchoredOnTheInitializedMember_NotOnTheContainingClass()
    {
        // The graph models which element owns a dependency, not runtime stack frames. The Creates used
        // to sit on the containing class, which kept the created type alive one level too coarse for
        // member-level analyses.
        var widget = Element("Widget", CodeElementType.Class);
        var factory = Element("Factory", CodeElementType.Class);
        var field = Element("Default", CodeElementType.Field);
        var property = Element("Prop", CodeElementType.Property);

        Assert.Multiple(() =>
        {
            Assert.That(HasEdge(field, widget, RelationshipType.Creates), Is.True);
            Assert.That(HasEdge(property, widget, RelationshipType.Creates), Is.True);
            Assert.That(HasEdge(factory, widget, RelationshipType.Creates), Is.False);
        });
    }

    [Test]
    public void ConstructorOnlyCalledFromFieldInitializers_IsNotDeadCode()
    {
        var reported = DeadCodeAnalysis.Calculate(_graph).Select(f => f.Element.Name).ToList();

        Assert.That(reported, Does.Not.Contain(".ctor"));
    }
}
