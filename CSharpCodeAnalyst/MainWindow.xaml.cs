using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using CSharpCodeAnalyst.Areas.GraphArea;
using CSharpCodeAnalyst.Areas.TreeArea;
using CSharpCodeAnalyst.Messages;

namespace CSharpCodeAnalyst;

public partial class MainWindow
{
    private readonly Dictionary<string, TreeViewItem> _codeElementIdToTreeViewItem = new();


    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        LeftExpander.Expanded += LeftExpander_Expanded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Set a fixed width for the left column on first load to prevent jumping
        EnsureLeftColumnWidth();
    }

    private void EnsureLeftColumnWidth()
    {
        // Use Dispatcher to ensure this runs after all layout updates
        Dispatcher.BeginInvoke(new Action(() =>
        {
            // Always ensure we have a fixed width to prevent jumping
            if (SplitterColumn.Width.IsAuto || SplitterColumn.Width.Value < Constants.TreeMinWidthExpanded)
            {
                SplitterColumn.Width = new GridLength(Constants.TreeMinWidthExpanded);
            }
        }), DispatcherPriority.Loaded);
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

        if (!treeViewItem.CanShowContextMenuForItem())
        {
            e.Handled = true; // Cancel the context menu
        }
    }

    private void LeftExpander_Collapsed(object sender, RoutedEventArgs e)
    {
        // When collapsed, set to auto but ensure it gets fixed when expanded again
        SplitterColumn.Width = GridLength.Auto;
    }

    private void LeftExpander_Expanded(object sender, RoutedEventArgs e)
    {
        // When expanded, ensure we have a proper fixed width to prevent jumping
        EnsureLeftColumnWidth();
    }

    private void GridSplitter_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var expander = LeftExpander;

        // Calculate the new width
        var newWidth = SplitterColumn.ActualWidth + e.HorizontalChange;

        // Set a minimum width (adjust as needed)
        var minWidth = expander.IsExpanded ? Constants.TreeMinWidthExpanded : Constants.TreeMinWidthCollapsed;

        if (newWidth < minWidth)
        {
            e.Handled = true;
            SplitterColumn.Width = new GridLength(minWidth);
        }
        else
        {
            SplitterColumn.Width = new GridLength(newWidth);
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

        CodeStructureTab.SelectedIndex = 0;

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

    private void RootWindow_Closing(object sender, CancelEventArgs e)
    {
        if (DataContext is MainViewModel mainVm)
        {
            e.Cancel = !mainVm.OnClosing();
        }
    }

    private void DropdownButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: not null } button
            && button.ContextMenu.Items[0] is MenuItem item)
        {
            button.ContextMenu.PlacementTarget = button;
            item.Tag = SearchDataGrid;
            button.ContextMenu.Placement = PlacementMode.Bottom;
            button.ContextMenu.IsOpen = true;
        }
    }

    public void SetViewer(GraphViewer explorationGraphViewer)
    {
        ExplorationControl.SetViewer(explorationGraphViewer);
    }
}