using Contracts.Graph;

namespace CSharpCodeAnalyst.GraphArea;

/// <summary>
///     Minimum state required to restore a graph session.
/// </summary>
[Serializable]
public class GraphSession
{
    public GraphSession()
    {
        Name = string.Empty;
        CodeElementIds = [];
        Relationships = [];
        PresentationState = new PresentationState();
    }

    private GraphSession(string name, List<string> codeElementIds, List<Relationship> relationships,
        PresentationState presentationState)
    {
        Name = name;
        CodeElementIds = codeElementIds;
        Relationships = relationships;
        PresentationState = presentationState;
    }

    public List<string> CodeElementIds { get; set; }
    public List<Relationship> Relationships { get; set; }
    public string Name { get; set; }
    public PresentationState PresentationState { get; set; }

    public static GraphSession Create(string name, CodeGraph codeGraph, PresentationState presentationState)
    {
        // No references in this state should be shared with the original state
        var codeElementIds = codeGraph.Nodes.Keys.ToList();
        var relationships = codeGraph.GetAllRelationships().ToList();
        var clonedPresentationState = presentationState.Clone();
        var sessionState = new GraphSession(name, codeElementIds, relationships, clonedPresentationState);
        return sessionState;
    }
}