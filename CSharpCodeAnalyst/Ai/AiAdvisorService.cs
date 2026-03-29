using CodeGraph.Algorithms.Cycles;
using CodeGraph.Export;
using CodeGraph.Graph;

namespace CSharpCodeAnalyst.Ai;

/// <summary>
///     Orchestrates AI-assisted cycle analysis: builds the prompt, calls the LLM, returns the Markdown response.
/// </summary>
public class AiAdvisorService
{
    private readonly AiClient _client = new();

    public async Task<string> GetCycleAdviceAsync(
        CycleGroup cycleGroup,
        string endpoint,
        string apiKey,
        string model,
        CancellationToken cancellationToken = default)
    {
        var level = GetCycleLevel(cycleGroup.CodeGraph);
        var serialized = CodeGraphSerializer.Serialize(cycleGroup.CodeGraph);
        var prompt = BuildCyclePrompt(level, serialized);
        return await _client.SendAsync(endpoint, apiKey, model, prompt, cancellationToken);
    }

    private static string GetCycleLevel(CodeGraph.Graph.CodeGraph cycleGraph)
    {
        var types = cycleGraph.Nodes.Values
            .Where(n => !n.IsExternal)
            .Select(n => n.ElementType)
            .ToList();

        if (types.Any(t => t is CodeElementType.Method or CodeElementType.Property or CodeElementType.Field))
            return "method";

        if (types.Any(t => t is CodeElementType.Class or CodeElementType.Interface
                or CodeElementType.Struct or CodeElementType.Record or CodeElementType.Enum))
            return "class";

        return "namespace";
    }

    private static string BuildCyclePrompt(string level, string serializedGraph)
    {
        return $"""
            You are a software architect analyzing a C# dependency cycle.

            The cycle exists at the **{level}** level and is represented as a strongly connected component (SCC)
            — every element in the group can reach every other element through the dependency graph.

            ## Your task

            1. **Trace the cycles.** Identify the concrete dependency paths that form the loop(s).
               Name the specific elements involved (use their `name` or `full` attributes).
            2. **Characterize the coupling.** For each path, explain what kind of dependency it is
               (e.g. type reference, method call chain, inheritance, instantiation).
            3. **Propose targeted refactorings.** Each suggestion must reference the specific elements
               and relationships from this graph by name. Do not give generic software-engineering advice.
               Instead, explain exactly which dependency to cut or redirect, and how.

            ## Relationship types

            - `Calls` — a method or property invokes another method or property
            - `Uses` — a type references another type (field type, parameter type, return type, local variable)
            - `Inherits` — class inherits from a base class
            - `Implements` — class or struct implements an interface
            - `Instantiates` — a type creates an instance of another type

            ## Graph format

            ```
            CodeElementType Id [name=Name] [full=FullName] [parent=ParentId] [external]
            [loc=File:Line,Col]
            SourceId RelationshipType TargetId
            [loc=File:Line,Col]
            ```

            ## Cycle group

            {serializedGraph}
            """;
    }
}
