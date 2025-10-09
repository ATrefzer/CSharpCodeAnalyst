using System.IO;
using System.Windows.Input;
using Contracts.Graph;
using CSharpCodeAnalyst.Areas.GraphArea.Filtering;
using CSharpCodeAnalyst.Areas.GraphArea.RenderOptions;
using CSharpCodeAnalyst.Help;

namespace CSharpCodeAnalyst.Areas.GraphArea;

public interface IGraphViewer
{
    void ShowFlatGraph(bool value);
    void ShowInformationFlow(bool value);
    void AddToGraph(IEnumerable<CodeElement> originalCodeElements, IEnumerable<Relationship> newRelationships, bool addCollapsed);
    void DeleteFromGraph(HashSet<string> idsToRemove);
    void DeleteFromGraph(List<Relationship> relationships);

    void AddCommand(ICodeElementContextCommand command);
    void AddGlobalCommand(IGlobalCommand command);

    /// <summary>
    ///     Clear the internal code graph. The graph is empty after this.
    /// </summary>
    void Clear();

    /// <summary>
    ///     Renders the graph and re-layouts it.
    /// </summary>
    void Layout();

    /// <summary>
    ///     Note:
    ///     The IQuickInfoFactory may be initialized with a different code graph.
    ///     We need the original graph if it exists such that we can
    ///     access the full parent paths that may not be known in the graph.
    ///     The graph may not include all the parents that are known in the original graph.
    /// </summary>
    void SetQuickInfoFactory(IQuickInfoFactory factory);

    CodeGraph GetGraph();
    void UpdateRenderOption(RenderOption renderOption);
    void SaveToSvg(FileStream stream);
    void SetHighlightMode(HighlightMode valueMode);
    void ShowGlobalContextMenu();

    /// <summary>
    ///     Current content of the graph for persistence and undo/redo.
    /// </summary>
    GraphSession GetSession();

    void Collapse(string id);
    void Expand(string id);
    bool IsCollapsed(string id);

    /// <summary>
    ///     Undo and gallery.
    /// </summary>
    void LoadSession(List<CodeElement> codeElements, List<Relationship> relationships, PresentationState state);

    /// <summary>
    ///     Cycle groups, focus on selected elements
    /// </summary>
    void LoadSession(CodeGraph newGraph, PresentationState? presentationState);

    void AddCommand(IRelationshipContextCommand command);

    // Flags
    bool IsFlagged(string id);
    void ToggleFlag(string id);

    void ToggleFlag(string sourceId, string targetId, List<Relationship> relationships);

    void ClearAllFlags();

    // Search highlights
    void SetSearchHighlights(List<string> nodeIds);
    void ClearSearchHighlights();

    // Event for graph changes to notify search UI
    event Action<CodeGraph>? GraphChanged;
    bool TryHandleKeyEvent(Key key);

    void SetHideFilter(GraphHideFilter hideFilter);
    GraphHideFilter GetHideFilter();
}