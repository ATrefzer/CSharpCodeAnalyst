using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.CodeGraph.Exploration;

internal class Context(Graph.CodeGraph codeGraph)
{
    public HashSet<CodeElement> ForbiddenCallSourcesInHierarchy { get; set; } = [];

    public bool IsCallAllowed(Relationship call)
    {
        if (ForbiddenCallSourcesInHierarchy.Count == 0)
        {
            return true; // No restrictions, all calls are allowed
        }

        // Implicit calls, "this" calls and "base" calls dispatch on the runtime type of "this",
        // which is always within the caller's own hierarchy. Check hierarchy restrictions.
        if (call.Attributes == RelationshipAttribute.None ||
            call.HasAttribute(RelationshipAttribute.IsBaseCall) ||
            call.HasAttribute(RelationshipAttribute.IsThisCall))
        {
            var sourceMethod = codeGraph.Nodes[call.SourceId];
            var sourceClass = CodeGraphExplorer.GetMethodContainer(sourceMethod);

            if (sourceClass != null && ForbiddenCallSourcesInHierarchy.Contains(sourceClass))
            {
                return false;
            }
        }

        // For instance calls, static calls and extension method calls,
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