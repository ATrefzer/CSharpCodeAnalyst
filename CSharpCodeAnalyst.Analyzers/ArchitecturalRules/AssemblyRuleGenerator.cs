using System.Text;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules;

/// <summary>
///     Generates a starting set of architectural rules from the current code graph, on assembly
///     level. It simply freezes today's dependency structure - no implicit assumptions: every
///     internal assembly may only depend on exactly the assemblies it depends on right now.
///
///     Per internal assembly:
///     <list type="bullet">
///         <item>no dependency on another internal assembly - <c>ISOLATE</c></item>
///         <item>otherwise a <c>RESTRICT</c> to each assembly it currently depends on</item>
///     </list>
///
///     The generated rules validate clean against the current graph; only *new* inter-assembly
///     dependencies get reported afterwards. Only internal assemblies are considered. If external
///     code was imported, ISOLATE / RESTRICT rules may additionally report framework usage - adjust
///     them after generation.
/// </summary>
public static class AssemblyRuleGenerator
{
    public static string Generate(CodeGraph.Graph.CodeGraph graph)
    {
        var assemblies = graph.Nodes.Values
            .Where(n => n.ElementType == CodeElementType.Assembly && !n.IsExternal)
            .OrderBy(n => n.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // For each assembly the set of internal assemblies it depends on.
        var dependsOn = assemblies.ToDictionary(a => a.Id, _ => new HashSet<string>());

        foreach (var relationship in graph.GetAllRelationships().Where(r => r.Type.IsDependency()))
        {
            if (!graph.Nodes.TryGetValue(relationship.SourceId, out var source) ||
                !graph.Nodes.TryGetValue(relationship.TargetId, out var target))
            {
                continue;
            }

            var sourceAssembly = AssemblyOf(source);
            var targetAssembly = AssemblyOf(target);
            if (sourceAssembly == null || targetAssembly == null ||
                sourceAssembly.IsExternal || targetAssembly.IsExternal ||
                sourceAssembly.Id == targetAssembly.Id)
            {
                continue;
            }

            dependsOn[sourceAssembly.Id].Add(targetAssembly.Id);
        }

        var sb = new StringBuilder();
        foreach (var assembly in assemblies)
        {
            var deps = dependsOn[assembly.Id];

            if (deps.Count == 0)
            {
                sb.AppendLine($"ISOLATE {assembly.FullName}.**");
                continue;
            }

            // Freeze the current dependencies: the assembly may only depend on exactly these.
            var targets = deps.Select(id => graph.Nodes[id])
                .OrderBy(t => t.FullName, StringComparer.OrdinalIgnoreCase);
            foreach (var target in targets)
            {
                sb.AppendLine($"RESTRICT {assembly.FullName}.** -> {target.FullName}.**");
            }
        }

        return sb.ToString();
    }

    private static CodeElement? AssemblyOf(CodeElement element)
    {
        var current = element;
        while (current != null)
        {
            if (current.ElementType == CodeElementType.Assembly)
            {
                return current;
            }

            current = current.Parent;
        }

        return null;
    }
}
