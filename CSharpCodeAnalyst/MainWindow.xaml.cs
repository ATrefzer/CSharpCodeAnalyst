using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.GraphArea;
using CSharpCodeAnalyst.TreeArea;

namespace CSharpCodeAnalyst;

public partial class MainWindow
{
    private readonly Dictionary<string, TreeViewItem> _codeElementIdToTreeViewItem = new();

    public MainWindow()
    {
        InitializeComponent();
    }

    public void SetViewer(IGraphBinding explorationGraphViewer
    )
    {
        explorationGraphViewer.Bind(ExplorationGraphPanel);
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TreeViewItem_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TreeViewItem treeViewItem)
        {
            return;
        }

        treeViewItem.Focus();
        treeViewItem.IsSelected = true;
        e.Handled = true;
    }

    private void TreeView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.DataContext is not TreeItemViewModel treeViewItem)
        {
            e.Handled = true; // Cancel the context menu
            return;
        }

        if (treeViewItem.CanShowContextMenuForItem() is false)
        {
            e.Handled = true; // Cancel the context menu
        }
    }

    private void LeftExpander_Collapsed(object sender, RoutedEventArgs e)
    {
        // Once we move the splitter around the width is no longer auto
        LeftColumn.Width = GridLength.Auto;
    }

    private void GridSplitter_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var expander = LeftExpander;
        var leftColumn = SplitterGrid.ColumnDefinitions[0];

        // Calculate the new width
        var newWidth = leftColumn.ActualWidth + e.HorizontalChange;

        // Set a minimum width (adjust as needed)
        var minWidth = expander.IsExpanded ? Constants.TreeMinWidthExpanded : Constants.TreeMinWidthCollapsed;

        if (newWidth < minWidth)
        {
            e.Handled = true;
            leftColumn.Width = new GridLength(minWidth);
        }
        else
        {
            leftColumn.Width = new GridLength(newWidth);
        }
    }

    private void TreeViewItem_Loaded(object sender, RoutedEventArgs e)
    {
        // Called then the tree view item is loaded into the visual tree.
        if (sender is TreeViewItem { DataContext: TreeItemViewModel { CodeElement: not null } viewModel } treeViewItem)
        {
            _codeElementIdToTreeViewItem[viewModel.CodeElement.Id] = treeViewItem;
        }
    }

    private void TreeViewItem_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is TreeViewItem { DataContext: TreeItemViewModel { CodeElement: not null } viewModel })
        {
            _codeElementIdToTreeViewItem.Remove(viewModel.CodeElement.Id);
        }
        else
        {
            // We get a disconnected item if a new project is loaded.
            _codeElementIdToTreeViewItem.Clear();
        }
    }

    /// <summary>
    ///     Whenever a TreeViewItem is loaded into the visual tree we capture it in the Load event
    /// </summary>
    public void HandleLocateInTreeRequest(LocateInTreeRequest request)
    {
        var treeViewModel = CodeTree.DataContext as TreeViewModel;
        if (treeViewModel == null)
        {
            return;
        }

        // Causes TreeViewItem_Loaded
        treeViewModel.ExpandParents(request.Id);

        // Use Dispatcher to ensure UI is updated before bringing item into view
        Dispatcher.InvokeAsync(() =>
        {
            if (_codeElementIdToTreeViewItem.TryGetValue(request.Id, out var tvi))
            {
                tvi.BringIntoView();
                tvi.Focus();
            }
        }, DispatcherPriority.Render);
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

    private void RootWindow_Closing(object sender, CancelEventArgs e)
    {
        if (DataContext is MainViewModel mainVm)
        {
            e.Cancel = !mainVm.OnClosing();
        }
    }
}