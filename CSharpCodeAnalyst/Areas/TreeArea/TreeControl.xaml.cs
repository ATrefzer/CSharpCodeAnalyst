using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Messages;

namespace CSharpCodeAnalyst.Areas.TreeArea;

public partial class TreeControl : UserControl
{
    private readonly Dictionary<string, TreeViewItem> _codeElementIdToTreeViewItem = new();
    public TreeControl()
    {
        InitializeComponent();
    }
    
    /// <summary>
    ///     Whenever a TreeViewItem is loaded into the visual tree we capture it in the Load event
    /// </summary>
    public void HandleLocateInTreeRequest(LocateInTreeRequest request)
    {
        if (DataContext is not TreeViewModel treeViewModel)
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
        var treeView = sender as TreeView;
        if (treeView == null)
        {
            return;
        }

        if (CodeTree.Items.Count == 0)
        {
            // Don't show context menu if tree is empty
            e.Handled = true;
            return;
        }

        // Check if we clicked on a TreeViewItem or empty space
        if ((e.OriginalSource as FrameworkElement)?.DataContext is not TreeItemViewModel treeViewModel)
        {
            // Clicked on empty space - show root-level context menu
            ShowRootContextMenu(treeView, e);
            e.Handled = true;
            return;
        }

        // Clicked on an item - use the normal context menu
        if (!treeViewModel.CanShowContextMenuForItem())
        {
            e.Handled = true; // Cancel the context menu
        }
    }

    private void ShowRootContextMenu(TreeView treeView, ContextMenuEventArgs e)
    {
        // Get the TreeViewModel from DataContext
        if (treeView.DataContext is not TreeViewModel treeViewModel)
        {
            e.Handled = true;
            return;
        }

        // Yes, we can create an own context menu on the fly
        var emptyContextMenu = new ContextMenu();
        var refactoringMenu = new MenuItem
        {
            Header = Strings.Refactor
        };

        var createMenuItem = new MenuItem
        {
            Header = Strings.Refactor_CreateCodeElement,
        };

        // Command binding has issues with null parameters
        createMenuItem.Click += (s, args) =>
        {
            treeViewModel.CreateCodeElementAtRoot();
        };

        emptyContextMenu.Items.Add(refactoringMenu);
        refactoringMenu.Items.Add(createMenuItem);

        // Position and show the context menu
        emptyContextMenu.PlacementTarget = treeView;
        emptyContextMenu.IsOpen = true;
    }
}