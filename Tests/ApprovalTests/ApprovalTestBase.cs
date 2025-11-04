using CodeParser.Extensions;
using CodeParser.Parser;
using CodeParser.Parser.Config;
using Contracts.Graph;

namespace CodeParserTests.ApprovalTests;

public class ResolvedRelationship
{
    public ResolvedRelationship(string source, string target)
    {
        Source = source;
        Target = target;
    }

    public string Source { get; }
    public string Target { get; }

    public override string ToString()
    {
        return $"{Source} -> {Target}";
    }

    public override bool Equals(object? obj)
    {
        return Source == Target;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Source, Target);
    }
}

internal static class Init
{
    private static CodeGraph? _instance;

    static Init()
    {
        // Run exactly once, before any tests
        Initializer.InitializeMsBuildLocator();
    }

    public static async Task<CodeGraph> LoadAsync()
    {
        if (_instance == null)
        {
            var parser = new Parser(new ParserConfig(new ProjectExclusionRegExCollection(), false));
            _instance = await parser.ParseAsync(@"..\..\..\..\TestSuite\TestSuite.sln");
        }


        return _instance;
    }
}

/// <summary>
///     Base class for project-focused approval tests.
///     Provides common filtering and assertion methods.
/// </summary>
public abstract class ApprovalTestBase
{

    protected CodeGraph Graph = null!;


    public static string DumpRelationships(HashSet<string> relationships)
    {
        var formattedRelationships = string.Join(",\n", relationships.Select(s => $"\"{s}\""));
        return formattedRelationships;
    }

    public static string DumpCodeElements(HashSet<string> nodes)
    {
        var formattedNodes = string.Join(",\n", nodes.Select(n => $"\"{n}\""));
        return formattedNodes;
    }

    protected HashSet<string> GetAllStructs(CodeGraph graph)
    {
        return GetElementOfType(graph, CodeElementType.Struct);
    }

    protected HashSet<string> GetAllEnums(CodeGraph graph)
    {
        return GetElementOfType(graph, CodeElementType.Enum);
    }

    protected HashSet<string> GetAllProperties(CodeGraph graph)
    {
        return GetElementOfType(graph, CodeElementType.Property);
    }

    protected bool IsInProject(Relationship relationship, string projectFilter)
    {
        return Graph.Nodes[relationship.SourceId].FullName.StartsWith(projectFilter) && Graph.Nodes[relationship.TargetId].FullName.StartsWith(projectFilter);
    }

    protected bool IsInProject(CodeElement element, string projectFilter)
    {
        return element.FullName.StartsWith(projectFilter);
    }

    protected CodeGraph GetTestGraph(string rootElementName)
    {
        var root = Graph.Nodes.Values.Single(n => n.FullName == rootElementName);
        return Graph.SubGraphOf(root);
    }

    [OneTimeSetUp]
    public async Task FixtureSetup()
    {
        Graph = await Init.LoadAsync();
    }

    protected HashSet<string> GetAllClasses(CodeGraph graph)
    {
        return GetElementOfType(graph, CodeElementType.Class);
    }

    protected HashSet<string> GetAllClasses(CodeGraph graph, string inNamespace)
    {
        return GetElementOfType(graph, CodeElementType.Class, inNamespace);
    }

    protected static HashSet<string> GetAllNodes(CodeGraph graph)
    {
        return graph.Nodes.Values
            .Select(n => n.FullName)
            .ToHashSet();
    }

    protected static HashSet<string> GetAllNodesOfType(CodeGraph graph, CodeElementType type)
    {
        return graph.Nodes.Values.Where(n => n.ElementType == type)
            .Select(n => n.FullName)
            .ToHashSet();
    }

    private static HashSet<string> GetElementOfType(CodeGraph graph, CodeElementType type, string? inNamespace = null!)
    {
        var elements = graph.Nodes.Values
            .Where(n => n.ElementType == type);

        if (inNamespace != null)
        {
            elements = elements.Where(n => n.Parent?.FullName == inNamespace);
        }

        return elements.Select(n => n.FullName)
            .ToHashSet();
    }

    public HashSet<string> GetAllMethodGroupUsages(CodeGraph graph)
    {
        return graph.GetAllRelationships()
            .Where(r => r.Type == RelationshipType.Uses && r.Attributes.HasFlag(RelationshipAttribute.IsMethodGroup))
            .Select(CreateResolvedRelationShip)
            .Select(r => $"{r.Source} -> {r.Target}")
            .ToHashSet();
    }

    public HashSet<string> GetRelationshipsOfType(CodeGraph graph, RelationshipType type)
    {
        return graph.GetAllRelationships()
            .Where(r => r.Type == type)
            .Select(CreateResolvedRelationShip)
            .Select(r => $"{r.Source} -> {r.Target}")
            .ToHashSet();
    }

    protected HashSet<string> GetAllRelationships(CodeGraph graph)
    {
        return graph.GetAllRelationships()
            .Select(CreateResolvedRelationShip)
            .Select(r => $"{r.Source} -> {r.Target}")
            .ToHashSet();
    }




    public ResolvedRelationship CreateResolvedRelationShip(Relationship relationship)
    {
        return new ResolvedRelationship(Graph.Nodes[relationship.SourceId].FullName, Graph.Nodes[relationship.TargetId].FullName);
    }

    public static HashSet<string> GetAllEventImplementations(CodeGraph graph)
    {
        var actual = graph.Nodes.Values
            .SelectMany(n => n.Relationships)
            .Where(d => d.Type == RelationshipType.Implements)
            .Select(d => (graph.Nodes[d.SourceId], graph.Nodes[d.TargetId]))
            .Where(t => t.Item1.ElementType == CodeElementType.Event &&
                        t.Item2.ElementType == CodeElementType.Event)
            .Select(t => $"{t.Item1.FullName} -> {t.Item2.FullName}")
            .ToHashSet();
        return actual;
    }

    public static HashSet<string> GetAllPropertyOverrides(CodeGraph graph)
    {
        var actual = graph.Nodes.Values
            .SelectMany(n => n.Relationships)
            .Where(d => d.Type == RelationshipType.Overrides)
            .Select(d => (graph.Nodes[d.SourceId], graph.Nodes[d.TargetId]))
            .Where(t => t.Item1.ElementType == CodeElementType.Property &&
                        t.Item2.ElementType == CodeElementType.Property)
            .Select(t => $"{t.Item1.FullName} -> {t.Item2.FullName}")
            .ToHashSet();
        return actual;
    }

    protected static HashSet<string> GetAllPropertyImplementations(CodeGraph graph)
    {
        var actual = graph.Nodes.Values
            .SelectMany(n => n.Relationships)
            .Where(d => d.Type == RelationshipType.Implements)
            .Select(d => (graph.Nodes[d.SourceId], graph.Nodes[d.TargetId]))
            .Where(t => t.Item1.ElementType == CodeElementType.Property &&
                        t.Item2.ElementType == CodeElementType.Property)
            .Select(t => $"{t.Item1.FullName} -> {t.Item2.FullName}")
            .ToHashSet();
        return actual;
    }

    protected static HashSet<string> GetAllMethodImplementations(CodeGraph graph)
    {
        var actual = graph.Nodes.Values
            .SelectMany(n => n.Relationships)
            .Where(d => d.Type == RelationshipType.Implements)
            .Select(d => (graph.Nodes[d.SourceId], graph.Nodes[d.TargetId]))
            .Where(t => t.Item1.ElementType == CodeElementType.Method &&
                        t.Item2.ElementType == CodeElementType.Method)
            .Select(t => $"{t.Item1.FullName} -> {t.Item2.FullName}")
            .ToHashSet();
        return actual;
    }

    protected static HashSet<string> GetAllMethodOverrides(CodeGraph graph)
    {
        var actual = graph.Nodes.Values
            .SelectMany(n => n.Relationships)
            .Where(d => d.Type == RelationshipType.Overrides)
            .Select(d => (graph.Nodes[d.SourceId], graph.Nodes[d.TargetId]))
            .Where(t => t.Item1.ElementType == CodeElementType.Method &&
                        t.Item2.ElementType == CodeElementType.Method)
            .Select(t => $"{t.Item1.FullName} -> {t.Item2.FullName}")
            .ToHashSet();
        return actual;
    }

    public static HashSet<string> GetAllEventInvocations(CodeGraph graph)
    {
        var actual = graph.Nodes.Values
            .SelectMany(n => n.Relationships)
            .Where(d => d.Type == RelationshipType.Invokes)
            .Select(d => (graph.Nodes[d.SourceId], graph.Nodes[d.TargetId]))
            .Select(t => $"{t.Item1.FullName} -> {t.Item2.FullName}")
            .ToHashSet();
        return actual;
    }

    protected static HashSet<string> GetAllInterfaceImplementations(CodeGraph graph)
    {
        var actual = graph.Nodes.Values
            .SelectMany(n => n.Relationships)
            .Where(d => d.Type == RelationshipType.Implements)
            .Select(d => (graph.Nodes[d.SourceId], graph.Nodes[d.TargetId]))
            .Where(t => (t.Item1.ElementType == CodeElementType.Class ||
                         t.Item1.ElementType == CodeElementType.Interface) &&
                        t.Item2.ElementType == CodeElementType.Interface)
            .Select(t => $"{t.Item1.FullName} -> {t.Item2.FullName}")
            .ToHashSet();
        return actual;
    }

    protected static HashSet<string> GetAllClassInheritance(CodeGraph graph)
    {
        var actual = graph.Nodes.Values
            .SelectMany(n => n.Relationships)
            .Where(d => d.Type == RelationshipType.Inherits)
            .Select(d => (graph.Nodes[d.SourceId], graph.Nodes[d.TargetId]))
            .Where(t => t.Item1.ElementType == CodeElementType.Class &&
                        t.Item2.ElementType == CodeElementType.Class)
            .Select(t => $"{t.Item1.FullName} -> {t.Item2.FullName}")
            .ToHashSet();
        return actual;
    }
}