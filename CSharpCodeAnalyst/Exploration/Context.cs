using Contracts.Graph;

namespace CSharpCodeAnalyst.Exploration;

internal class Context(CodeGraph codeGraph)
{
    public HashSet<CodeElement> ForbiddenCallSourcesInHierarchy { get; set; } = [];

    public bool IsCallAllowed(Relationship call)
    {
        if (ForbiddenCallSourcesInHierarchy.Count == 0)
        {
            return true; // No restrictions, all calls are allowed
        }

        // For normal calls (not instance calls), check hierarchy restrictions
        if (call.Attributes == RelationshipAttribute.None || call.HasAttribute(RelationshipAttribute.IsBaseCall))
        {
            var sourceMethod = codeGraph.Nodes[call.SourceId];
            var sourceClass = CodeGraphExplorer.GetMethodContainer(sourceMethod);

            if (sourceClass != null && ForbiddenCallSourcesInHierarchy.Contains(sourceClass))
            {
                return false;
            }
        }

        // For instance calls, static calls, extension method calls, and this calls,
        // allow them regardless of hierarchy restrictions
        return true;
    }

    public Context Clone()
    {
        var clone = new Context(codeGraph)
        {
            ForbiddenCallSourcesInHierarchy = ForbiddenCallSourcesInHierarchy.ToHashSet()
        };
        return clone;
    }
}