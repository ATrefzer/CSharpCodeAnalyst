using Contracts.Graph;

namespace CSharpCodeAnalyst.GraphArea;

/// <summary>
/// Minimum state required to restore a graph session.
/// </summary>
[Serializable]
public class GraphSessionState
{
    public GraphSessionState()
    {
        Name = string.Empty;
        CodeElementIds = [];
        Dependencies = [];
        PresentationState = new();
    }

    private GraphSessionState(string name, List<string> codeElementIds, List<Dependency> dependencies,
        PresentationState presentationState)
    {
        Name = name;
        CodeElementIds = codeElementIds;
        Dependencies = dependencies;
        PresentationState = presentationState;
    }

    public List<string> CodeElementIds { get; set; }
    public List<Dependency> Dependencies { get; set; }
    public string Name { get; set; }
    public PresentationState PresentationState { get; set; }

    public static GraphSessionState Create(string name, CodeGraph codeGraph, PresentationState presentationState)
    {
        // No references in this state should be shared with the original state
        var codeElementIds = codeGraph.Nodes.Keys.ToList();
        var dependencies = codeGraph.GetAllDependencies().ToList();
        var clonedPresentationState = presentationState.Clone();
        var sessionState = new GraphSessionState(name, codeElementIds, dependencies, clonedPresentationState);
        return sessionState;
    }
}