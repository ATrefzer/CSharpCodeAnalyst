using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using CSharpCodeAnalyst.Areas.GraphArea;
using CSharpCodeAnalyst.Messages;
using CSharpCodeAnalyst.Shared.UI;

namespace CSharpCodeAnalyst;

public partial class MainWindow
{
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

        // Initialize Toast Manager with our canvas
        ToastManager.Initialize(ToastContainer);
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

    public void HandleLocateInTreeRequest(LocateInTreeRequest request)
    {
        CodeStructureTab.SelectedIndex = 0;
        TreeControl.HandleLocateInTreeRequest(request);
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

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (WorkingArea.SelectedIndex == 0)
        {
            // Code explorer
            var mainVm = ExplorationControl.DataContext as MainViewModel;
            var graphVm = mainVm?.GraphViewModel;
            if (graphVm != null && graphVm.TryHandleKeyDown(e))
            {
                e.Handled = true;
            }
        }
    }
}