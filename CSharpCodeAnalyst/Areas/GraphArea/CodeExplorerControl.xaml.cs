using System.Windows.Controls;
using System.Windows.Input;
using CSharpCodeAnalyst.GraphArea;

namespace CSharpCodeAnalyst.Areas.GraphArea
{
    /// <summary>
    ///     Interaction logic for CodeExplorerControl.xaml
    /// </summary>
    public partial class CodeExplorerControl
    {
        public CodeExplorerControl()
        {
            InitializeComponent();
        }

        public void SetViewer(IGraphBinding graphViewer
        )
        {
            graphViewer.Bind(GraphPanel);
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
}