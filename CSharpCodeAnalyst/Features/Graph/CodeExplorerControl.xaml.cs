using System.Windows.Input;
using CSharpCodeAnalyst.Shared.Contracts;
using CSharpCodeAnalyst.Shared.Messages;

namespace CSharpCodeAnalyst.Features.Graph;

public partial class CodeExplorerControl
{
    private IPublisher? _publisher;

    public CodeExplorerControl()
    {
        InitializeComponent();
    }

    public void SetViewer(IGraphBinding graphViewer, IPublisher publisher, GraphSearchViewModel searchViewModel)
    {
        _publisher = publisher;
        graphViewer.Bind(GraphPanel);

        // The graph search is shared with the web view (it acts on the same GraphViewState).
        SearchControl.DataContext = searchViewModel;
    }

    private void OnMouseButtonDown(object sender, MouseButtonEventArgs e)
    {
        // If this is called no viewer object was hit. If an object was clicked while the info area is not visible
        // it is ignored, but the viewer locks the last clicked object.
        _publisher?.Publish(new ClearQuickInfoRequest());
    }
}