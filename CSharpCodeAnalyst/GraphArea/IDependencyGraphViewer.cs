using System.IO;
using Contracts.Graph;
using CSharpCodeAnalyst.GraphArea.RenderOptions;
using CSharpCodeAnalyst.Help;

namespace CSharpCodeAnalyst.GraphArea;

internal interface IDependencyGraphViewer
{
    event EventHandler BeforeChange;

    void ShowFlatGraph(bool value);
    void AddToGraph(IEnumerable<CodeElement> originalCodeElements, IEnumerable<Dependency> dependencies);
    void DeleteFromGraph(HashSet<string> idsToRemove);
    void AddDynamicContextCommand(IContextCommand command);
    void AddStaticContextCommand(IContextCommand command);

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
    void ImportCycleGroup(List<CodeElement> codeElements, List<Dependency> dependencies);

    /// <summary>
    ///     Current content of the graph for persistence and undo/redo.
    /// </summary>
    GraphSessionState GetSessionState();

    void RestoreSession(List<CodeElement> codeElements, List<Dependency> dependencies, PresentationState state);

    void Collapse(string id);
    void Expand(string id);
    bool IsCollapsed(string id);
}