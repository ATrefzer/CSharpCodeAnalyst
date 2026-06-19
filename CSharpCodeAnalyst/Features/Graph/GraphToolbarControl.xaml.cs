namespace CSharpCodeAnalyst.Features.Graph;

/// <summary>
///     Reusable strip of global graph-tool buttons. Render-agnostic — it only binds to
///     <c>GraphViewModel</c> commands, so it is hosted by both the MSAGL view
///     (<c>CodeExplorerControl</c>, floating) and the web view (<c>WebGraphControl</c>, docked).
/// </summary>
public partial class GraphToolbarControl
{
    public GraphToolbarControl()
    {
        InitializeComponent();
    }
}
