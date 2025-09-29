using System.Windows.Input;

namespace CSharpCodeAnalyst.Areas.GraphArea;

public partial class CodeExplorerControl
{
    private GraphSearchViewModel? _searchViewModel;

    public CodeExplorerControl()
    {
        InitializeComponent();
    }

    public void SetViewer(IGraphBinding graphViewer)
    {
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
        // Better user experience.
        // Allow context menu in space not occupied by the graph canvas
        if (DataContext is MainViewModel mainVm && e is
            {
                ButtonState: MouseButtonState.Pressed,
                ChangedButton: MouseButton.Right
            })
        {
            mainVm.GraphViewModel?.ShowGlobalContextMenu();
        }
    }
}