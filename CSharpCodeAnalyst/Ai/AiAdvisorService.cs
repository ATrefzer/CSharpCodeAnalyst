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
            Here is a cycle group extracted from C# source code.

            The cycle occurs on the {level} level.

            In graph theory terms this is a strongly connected component.

            The graph is in the following format (plain text, human readable form):

            CodeElementType Id [ name=Name] [ full=FullName] [ parent=ParentId] [ external] [ attr=Attr1,Attr2]
            [loc=File:Line,Col]*
            SourceId RelationshipType TargetId [ Attr1,Attr2]
            [loc=File:Line,Col]*

            Please come up with ideas on how this cycle group can be removed or at least broken down into smaller parts.
            Provide your answer as markdown.

            The cycle group starts here:

            {serializedGraph}
            """;
    }
}
