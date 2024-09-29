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
        Dependencies = [];
        PresentationState = new PresentationState();
    }

    private GraphSession(string name, List<string> codeElementIds, List<Relationship> dependencies,
        PresentationState presentationState)
    {
        Name = name;
        CodeElementIds = codeElementIds;
        Dependencies = dependencies;
        PresentationState = presentationState;
    }

    public List<string> CodeElementIds { get; set; }
    public List<Relationship> Dependencies { get; set; }
    public string Name { get; set; }
    public PresentationState PresentationState { get; set; }

    public static GraphSession Create(string name, CodeGraph codeGraph, PresentationState presentationState)
    {
        // No references in this state should be shared with the original state
        var codeElementIds = codeGraph.Nodes.Keys.ToList();
        var dependencies = codeGraph.GetAllRelationships().ToList();
        var clonedPresentationState = presentationState.Clone();
        var sessionState = new GraphSession(name, codeElementIds, dependencies, clonedPresentationState);
        return sessionState;
    }
}