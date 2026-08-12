using System.Text;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules;

/// <summary>
///     Generates a starting set of architectural rules from the current code graph, on assembly
///     level. It simply freezes today's dependency structure. Every
///     internal assembly may only depend on exactly the assemblies it depends on right now.
///     Per internal assembly:
///     <list type="bullet">
///         <item>no dependency on another internal assembly - <c>ISOLATE</c></item>
///         <item>otherwise a <c>RESTRICT</c> to each assembly it currently depends on</item>
///     </list>
///     The generated rules validate clean against the current graph
///     External code is no concern: RESTRICT and ISOLATE ignore dependencies to external elements.
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
        var dependsOn = CalculateDependenciesBetweenAssemblies(graph, assemblies);

        return GenerateRules(graph, assemblies, dependsOn);
    }

    private static string GenerateRules(CodeGraph.Graph.CodeGraph graph, List<CodeElement> assemblies, Dictionary<string, HashSet<string>> dependsOn)
    {
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
            var targets = deps
                .Select(id => graph.Nodes[id])
                .OrderBy(t => t.FullName, StringComparer.OrdinalIgnoreCase);

            foreach (var target in targets)
            {
                sb.AppendLine($"RESTRICT {assembly.FullName}.** -> {target.FullName}.**");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Calculates the mapping: Assembly id -> {Assembly ids}
    /// </summary>
    private static Dictionary<string, HashSet<string>> CalculateDependenciesBetweenAssemblies(CodeGraph.Graph.CodeGraph graph, List<CodeElement> assemblies)
    {
        var dependsOn = assemblies.ToDictionary(a => a.Id, _ => new HashSet<string>());

        foreach (var relationship in graph.GetAllRelationships().Where(r => r.Type.IsDependency()))
        {
            if (!graph.Nodes.TryGetValue(relationship.SourceId, out var source) ||
                !graph.Nodes.TryGetValue(relationship.TargetId, out var target))
            {
                continue;
            }

            var sourceAssembly = source.AssemblyOf();
            var targetAssembly = target.AssemblyOf();
            if (sourceAssembly == null || targetAssembly == null ||
                sourceAssembly.IsExternal || targetAssembly.IsExternal ||
                sourceAssembly.Id == targetAssembly.Id)
            {
                continue;
            }

            dependsOn[sourceAssembly.Id].Add(targetAssembly.Id);
        }

        return dependsOn;
    }
}