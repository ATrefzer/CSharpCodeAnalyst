using CodeGraph.Graph;
using CSharpCodeAnalyst.Features.Graph.Filtering;
using CSharpCodeAnalyst.Features.Graph.RenderOptions;

namespace CSharpCodeAnalyst.Features.Graph;

/// <summary>
///     Render-agnostic model of the graph view: the working graph, presentation state
///     (collapsed / flags / search highlights), hide filter, display toggles, highlight
///     mode, the context-command registry and session handling.
///
///     This is the single source of truth that both render adapters (the MSAGL
///     <see cref="GraphViewer" /> and the web graph control) observe via <see cref="Changed" />
///     and drive via the operations below. It contains no UI / MSAGL / WebView code, so it
///     is unit-testable.
/// </summary>
public class GraphViewState
{
    private readonly List<IRelationshipContextCommand> _edgeCommands = [];
    private readonly List<IGlobalCommand> _globalCommands = [];
    private readonly List<ICodeElementContextCommand> _nodeCommands = [];

    public CodeGraph.Graph.CodeGraph CodeGraph { get; private set; } = new();
    public PresentationState PresentationState { get; private set; } = new();
    public GraphHideFilter HideFilter { get; private set; } = new();

    public bool ShowFlat { get; private set; }
    public bool ShowInformationFlow { get; private set; }
    public HighlightMode HighlightMode { get; private set; } = HighlightMode.EdgeHovered;

    private readonly HashSet<string> _selectedIds = [];

    /// <summary>
    ///     The canonical selection, fed by whichever render adapter the user interacts with
    ///     (the web view pushes it on selection change; the MSAGL view on mouse-up). Commands
    ///     that act on "the selected elements" read this instead of an adapter-specific source.
    /// </summary>
    public IReadOnlyCollection<string> SelectedIds => _selectedIds;

    public IReadOnlyList<ICodeElementContextCommand> NodeCommands => _nodeCommands;
    public IReadOnlyList<IRelationshipContextCommand> EdgeCommands => _edgeCommands;
    public IReadOnlyList<IGlobalCommand> GlobalCommands => _globalCommands;

    /// <summary>Structural change: observers should re-render (and possibly re-layout).</summary>
    public event Action? Changed;

    /// <summary>The hover-highlight mode changed (a view concern, not a structural change).</summary>
    public event Action<HighlightMode>? HighlightModeChanged;

    /// <summary>The selection changed (not a structural change; no re-layout needed).</summary>
    public event Action? SelectionChanged;

    /// <summary>
    ///     A decoration changed (flags / search highlights). These are PresentationState
    ///     overlays that only restyle existing elements — observers must NOT re-layout.
    /// </summary>
    public event Action? DecorationsChanged;

    // ---- Command registry ---------------------------------------------------
    public void AddCommand(ICodeElementContextCommand command)
    {
        _nodeCommands.Add(command);
    }

    public void AddCommand(IRelationshipContextCommand command)
    {
        _edgeCommands.Add(command);
    }

    public void AddGlobalCommand(IGlobalCommand command)
    {
        _globalCommands.Add(command);
    }

    // ---- Display toggles ----------------------------------------------------
    public void SetShowFlat(bool value)
    {
        ShowFlat = value;
        RaiseChanged();
    }

    public void SetShowInformationFlow(bool value)
    {
        ShowInformationFlow = value;
        RaiseChanged();
    }

    public void SetHighlightMode(HighlightMode mode)
    {
        HighlightMode = mode;
        HighlightModeChanged?.Invoke(mode);
    }

    public void SetHideFilter(GraphHideFilter filter)
    {
        HideFilter = filter;
        RaiseChanged();
    }

    // ---- Selection ----------------------------------------------------------
    public void SetSelection(IEnumerable<string> ids)
    {
        var incoming = ids.ToList();
        if (_selectedIds.SetEquals(incoming))
        {
            // No actual change — don't churn observers (e.g. MSAGL mouse-up that didn't select).
            return;
        }

        _selectedIds.Clear();
        _selectedIds.UnionWith(incoming);
        SelectionChanged?.Invoke();
    }

    // ---- Decorations (flags / search highlights) ----------------------------
    // The data lives in PresentationState; these ops mutate it and raise DecorationsChanged
    // so every adapter restyles WITHOUT a re-layout.
    public bool IsFlagged(string id)
    {
        return PresentationState.IsFlagged(id);
    }

    public void ToggleFlag(string id)
    {
        PresentationState.SetFlaggedState(id, !PresentationState.IsFlagged(id));
        DecorationsChanged?.Invoke();
    }

    public void ToggleFlag(string sourceId, string targetId)
    {
        var key = (sourceId, targetId);
        PresentationState.SetFlaggedState(key, !PresentationState.IsFlagged(key));
        DecorationsChanged?.Invoke();
    }

    public void ClearAllFlags()
    {
        PresentationState.ClearAllFlags();
        DecorationsChanged?.Invoke();
    }

    public void SetSearchHighlights(IEnumerable<string> nodeIds)
    {
        PresentationState.ClearAllSearchHighlights();
        foreach (var id in nodeIds)
        {
            PresentationState.SetSearchHighlightedState(id, true);
        }

        DecorationsChanged?.Invoke();
    }

    public void ClearSearchHighlights()
    {
        PresentationState.ClearAllSearchHighlights();
        DecorationsChanged?.Invoke();
    }

    // ---- Collapse / expand --------------------------------------------------
    public void Collapse(string id)
    {
        PresentationState.SetCollapsedState(id, true);
        RaiseChanged();
    }

    public void Expand(string id)
    {
        PresentationState.SetCollapsedState(id, false);
        RaiseChanged();
    }

    public bool IsCollapsed(string id)
    {
        return PresentationState.IsCollapsed(id);
    }

    // ---- Add / remove -------------------------------------------------------
    public List<CodeElement> AddToGraph(IEnumerable<CodeElement> originalCodeElements,
        IEnumerable<Relationship> newRelationships, bool addCollapsed)
    {
        var integrated = AddToGraphInternal(originalCodeElements, newRelationships);

        if (addCollapsed)
        {
            foreach (var codeElement in integrated.Where(c => c.Children.Any()))
            {
                PresentationState.SetCollapsedState(codeElement.Id, true);
            }
        }

        RaiseChanged();
        return integrated;
    }

    public void RemoveRelationships(List<Relationship> relationships)
    {
        foreach (var relationship in relationships)
        {
            CodeGraph.Nodes[relationship.SourceId].Relationships.Remove(relationship);
        }

        RaiseChanged();
    }

    public void RemoveElements(HashSet<string> idsToRemove)
    {
        if (idsToRemove.Count == 0)
        {
            return;
        }

        CodeGraph.RemoveCodeElements(idsToRemove);
        PresentationState.RemoveStates(idsToRemove);
        RaiseChanged();
    }

    // ---- Sessions -----------------------------------------------------------
    public GraphSession GetSession()
    {
        return GraphSession.Create("", CodeGraph, PresentationState);
    }

    public void LoadSession(List<CodeElement> codeElements, List<Relationship> relationships, PresentationState state)
    {
        ClearData();
        AddToGraphInternal(codeElements, relationships);
        PresentationState = state;
        RaiseChanged();
    }

    public void LoadSession(CodeGraph.Graph.CodeGraph newGraph, PresentationState? presentationState)
    {
        PresentationState = presentationState ?? new PresentationState();
        CodeGraph = newGraph;
        RaiseChanged();
    }

    public void Clear()
    {
        ClearData();
        RaiseChanged();
    }

    private void ClearData()
    {
        CodeGraph = new CodeGraph.Graph.CodeGraph();
        PresentationState = new PresentationState();
    }

    private List<CodeElement> AddToGraphInternal(IEnumerable<CodeElement> originalCodeElements,
        IEnumerable<Relationship> newRelationships)
    {
        var integrated = IntegrateNewFromOriginal(originalCodeElements);

        // Add relationships we explicitly requested.
        foreach (var newRelationship in newRelationships)
        {
            var sourceElement = CodeGraph.Nodes[newRelationship.SourceId];
            sourceElement.Relationships.Add(newRelationship);
        }

        return integrated;
    }

    /// <summary>
    ///     Adds the new nodes, integrating hierarchical relationships from original master
    ///     nodes. Parent / child connections not present in this graph are discarded.
    /// </summary>
    private List<CodeElement> IntegrateNewFromOriginal(IEnumerable<CodeElement> originalCodeElements)
    {
        var integrated = new List<CodeElement>();
        foreach (var originalElement in originalCodeElements)
        {
            var result = CodeGraph.IntegrateCodeElementFromOriginal(originalElement);
            if (result.IsAdded)
            {
                integrated.Add(result.CodeElement);
            }
        }

        return integrated;
    }

    private void RaiseChanged()
    {
        Changed?.Invoke();
    }
}
