using CodeGraph.Graph;
using CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     An event handler is registered in the method body (+= MyHandler) and unregistered inside a lambda
///     (x.MyEvent -= MyHandler). Both must be attributed to the same handler, yielding a single Handles
///     relationship carrying the registration and the unregistration attribute. Self-contained probe -
///     migrated from the former EventDeRegistrationInLambda approval fixture.
/// </summary>
[TestFixture]
public class EventDeRegistrationInLambdaParseTests
{
    private const string Code = """
                                using System;
                                using System.Collections.Generic;

                                namespace Demo;

                                public class Source
                                {
                                    public event EventHandler MyEvent;
                                }

                                public class EventDeRegistrationInLambda
                                {
                                    private void Do()
                                    {
                                        var source = new Source();
                                        source.MyEvent += MyHandler;

                                        List<Source> sources = [source];
                                        sources.LoopOver(x => x.MyEvent -= MyHandler);
                                    }

                                    private void MyHandler(object? sender, EventArgs e)
                                    {
                                    }
                                }

                                internal static class Extensions
                                {
                                    public static void LoopOver<T>(this IEnumerable<T> enumerable, Action<T> action)
                                    {
                                        foreach (var item in enumerable)
                                        {
                                            action(item);
                                        }
                                    }
                                }
                                """;

    private CodeGraph.Graph.CodeGraph _graph = null!;

    [OneTimeSetUp]
    public void Setup()
    {
        var parser = new CodeParser.Parser.Parser(new ParserConfig(new ProjectExclusionRegExCollection(), false));
        _graph = parser.ParseSourceAsync(Code).GetAwaiter().GetResult();
    }

    [Test]
    public void Handler_HasSingleHandlesEdge_WithRegistrationAndUnregistration()
    {
        var handler = _graph.Nodes.Values.Single(n =>
            n is { ElementType: CodeElementType.Method, Name: "MyHandler", Parent.Name: "EventDeRegistrationInLambda" });

        var handles = handler.Relationships.Single(r => r.Type == RelationshipType.Handles);

        Assert.Multiple(() =>
        {
            Assert.That(handles.HasAttribute(RelationshipAttribute.EventRegistration), Is.True);
            Assert.That(handles.HasAttribute(RelationshipAttribute.EventUnregistration), Is.True);
        });
    }

    [Test]
    public void Classes_AreDetected()
    {
        Assert.That(NamesOf(CodeElementType.Class),
            Is.EquivalentTo(new[] { "EventDeRegistrationInLambda", "Extensions", "Source" }));
    }

    [Test]
    public void TheOnlyCall_IsTheExtensionMethod()
    {
        var doMethod = _graph.Nodes.Values.Single(n => n.ElementType == CodeElementType.Method && n.Name == "Do");

        var callTargets = doMethod.Relationships
            .Where(r => r.Type == RelationshipType.Calls)
            .Select(r => _graph.Nodes[r.TargetId].Name);

        Assert.That(callTargets, Is.EquivalentTo(new[] { "LoopOver" }));
    }

    private string[] NamesOf(CodeElementType type)
    {
        return _graph.Nodes.Values.Where(n => n.ElementType == type).Select(n => n.Name).ToArray();
    }
}
