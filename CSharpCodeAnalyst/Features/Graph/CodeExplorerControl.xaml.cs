using System.Windows.Input;
using CSharpCodeAnalyst.Shared.Contracts;
using CSharpCodeAnalyst.Shared.Messages;

namespace CSharpCodeAnalyst.Features.Graph;

public partial class CodeExplorerControl
{
    private GraphSearchViewModel? _searchViewModel;
    private IPublisher? _publisher;

    public CodeExplorerControl()
    {
        InitializeComponent();
    }

    public void SetViewer(IGraphBinding graphViewer, IPublisher publisher)
    {
        _publisher = publisher;
        graphViewer.Bind(GraphPanel);

        // Initialize search functionality if the viewer implements IGraphViewer
        if (graphViewer is IGraphViewer viewer)
        {
            _searchViewModel = new GraphSearchViewModel(viewer);
            SearchControl.DataContext = _searchViewModel;
        }
    }

    private void OnMouseButtonDown(object sender, MouseButtonEventArgs e)
    {
        // If this is called no viewer object was hit. If an object was clicked while the info area is not visible
        // it is ignored, but the viewer locks the last clicked object.
        _publisher?.Publish(new ClearQuickInfoRequest());
    }
}