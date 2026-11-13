using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.Parser;

internal static class GraphInvariantAssertions
{
    public static void AssertValid(CodeGraph graph)
    {
        foreach (var (id, node) in graph.Nodes)
        {
            Assert.That(node.Id, Is.EqualTo(id));
            foreach (var edge in node.Relationships)
            {
                Assert.That(edge.SourceId, Is.EqualTo(id), $"Edge stored under wrong source: {node.FullName}");
                Assert.That(graph.Nodes.ContainsKey(edge.TargetId), Is.True, $"Dangling edge from {node.FullName}");
            }

            if (node.Parent is { } parent)
            {
                Assert.That(graph.Nodes.TryGetValue(parent.Id, out var registeredParent), Is.True);
                Assert.That(registeredParent, Is.SameAs(parent));
                Assert.That(parent.Children.Any(child => ReferenceEquals(child, node)), Is.True);
            }
            else
            {
                Assert.That(node.ElementType, Is.EqualTo(CodeElementType.Assembly), $"Unexpected root: {node.FullName}");
            }

            foreach (var child in node.Children)
            {
                Assert.That(graph.Nodes.TryGetValue(child.Id, out var registeredChild), Is.True);
                Assert.That(registeredChild, Is.SameAs(child));
                Assert.That(child.Parent, Is.SameAs(node));
            }

            var ancestors = new HashSet<string>();
            CodeElement? current = node;
            while (current is not null && ancestors.Add(current.Id))
            {
                current = current.Parent;
            }

            Assert.That(current, Is.Null, $"Hierarchy cycle starting at {node.FullName}");
        }
    }
}
