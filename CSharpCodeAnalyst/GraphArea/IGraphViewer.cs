using System.IO;
using Contracts.Graph;
using CSharpCodeAnalyst.GraphArea.RenderOptions;
using CSharpCodeAnalyst.Help;

namespace CSharpCodeAnalyst.GraphArea;

internal interface IGraphViewer
{
    void ShowFlatGraph(bool value);
    void AddToGraph(IEnumerable<CodeElement> originalCodeElements, IEnumerable<Relationship> newRelationships);
    void DeleteFromGraph(HashSet<string> idsToRemove);
    void DeleteFromGraph(List<Relationship> relationships);

    void AddContextMenuCommand(ICodeElementContextCommand command);
    void AddGlobalContextMenuCommand(IGlobalContextCommand command);

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
    ///     Cycle groups, focus on marked elements
    /// </summary>
    void LoadSession(CodeGraph newGraph, PresentationState? presentationState);

    void AddContextMenuCommand(IRelationshipContextCommand command);
}