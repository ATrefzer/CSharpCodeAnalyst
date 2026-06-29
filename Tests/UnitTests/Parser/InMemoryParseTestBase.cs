using CodeGraph.Graph;
using CodeParser.Parser.Config;

namespace CodeParserTests.UnitTests.Parser;

/// <summary>
///     Base class for self-contained in-memory parser tests: a fixture supplies a single C# snippet via
///     <see cref="Code" />, the base parses it once through the full pipeline (split OFF, to match the
///     former approval fixtures) and exposes path/relationship projections below the namespace level.
///     The <see cref="PathOf" /> projection strips the assembly and all namespace nodes, so it equals the
///     suffix the old fixtures had after the "&lt;Assembly&gt;.global.&lt;Namespace&gt;." prefix.
/// </summary>
public abstract class InMemoryParseTestBase
{
    protected CodeGraph.Graph.CodeGraph Graph = null!;

    /// <summary>The C# snippet to parse. One compilation unit; typically under <c>namespace Demo;</c>.</summary>
    protected abstract string Code { get; }

    /// <summary>Override to parse with the SplitPropertyAccessors option (get_/set_ accessor children).</summary>
    protected virtual bool SplitPropertyAccessors => false;

    [OneTimeSetUp]
    public void ParseCode()
    {
        var parser = new CodeParser.Parser.Parser(
            new ParserConfig(new ProjectExclusionRegExCollection(), false, splitPropertyAccessors: SplitPropertyAccessors));
        Graph = parser.ParseSourceAsync(Code).GetAwaiter().GetResult();
    }

    /// <summary>All element paths of the given type (e.g. every class as "Outer.Inner").</summary>
    protected string[] PathsOf(CodeElementType type)
    {
        return Graph.Nodes.Values.Where(n => n.ElementType == type).Select(PathOf).ToArray();
    }

    /// <summary>All relationships of the given type projected to "{source path} -> {target path}".</summary>
    protected string[] RelsOf(RelationshipType type)
    {
        return Graph.Nodes.Values
            .SelectMany(source => source.Relationships
                .Where(r => r.Type == type)
                .Select(r => $"{PathOf(source)} -> {PathOf(Graph.Nodes[r.TargetId])}"))
            .ToArray();
    }

    /// <summary>Method-group usages: Uses edges carrying the IsMethodGroup attribute (not Calls).</summary>
    protected string[] MethodGroupUsages()
    {
        return Graph.Nodes.Values
            .SelectMany(source => source.Relationships
                .Where(r => r.Type == RelationshipType.Uses && r.Attributes.HasFlag(RelationshipAttribute.IsMethodGroup))
                .Select(r => $"{PathOf(source)} -> {PathOf(Graph.Nodes[r.TargetId])}"))
            .ToArray();
    }

    /// <summary>Path of an element below the namespace: joins names up to (but excluding) the namespace/assembly.</summary>
    protected static string PathOf(CodeElement element)
    {
        var parts = new List<string>();
        var current = element;
        while (current is not null && current.ElementType is not (CodeElementType.Namespace or CodeElementType.Assembly))
        {
            parts.Insert(0, current.Name);
            current = current.Parent;
        }

        return string.Join(".", parts);
    }
}
