using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using System.Windows.Input;
using System.Windows.Threading;
using CSharpCodeAnalyst.Features.Graph;
using CSharpCodeAnalyst.Shared.Contracts;
using CSharpCodeAnalyst.Shared.Messages;
using CSharpCodeAnalyst.Shared.UI;

namespace CSharpCodeAnalyst;

public partial class MainWindow
{
    public const double TreeMinWidthCollapsed = 24;
    public const double TreeMinWidthExpanded = 400;

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
            if (SplitterColumn.Width.IsAuto || SplitterColumn.Width.Value < TreeMinWidthExpanded)
            {
                SplitterColumn.Width = new GridLength(TreeMinWidthExpanded);
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
        var minWidth = expander.IsExpanded ? TreeMinWidthExpanded : TreeMinWidthCollapsed;

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
        CodeStructureTab.SelectedIndex = TabIndices.Left.TreeView;
        TreeControl.HandleLocateInTreeRequest(request);
    }

    private void RootWindow_Closing(object sender, CancelEventArgs e)
    {
        if (DataContext is MainViewModel mainVm)
        {
            e.Cancel = !mainVm.OnClosing();
        }
    }

    public void SetViewer(GraphViewer explorationGraphViewer, GraphViewState graphViewState, IPublisher publisher,
        ISubscriber subscriber, GraphSearchViewModel graphSearchViewModel)
    {
        ExplorationControl.SetViewer(explorationGraphViewer, publisher, graphSearchViewModel);

        // The web view observes the same shared model directly (no MSAGL dependency) and
        // listens on the bus for render-only commands (Layout / Refit).
        WebGraphView.SetViewer(graphViewState, publisher, subscriber);

        // Both views host the same shared graph search (acts on the shared GraphViewState).
        WebGraphSearch.DataContext = graphSearchViewModel;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (WorkingArea.SelectedIndex == TabIndices.Right.CodeExplorer)
        {
            var mainVm = ExplorationControl.DataContext as MainViewModel;
            var graphVm = mainVm?.GraphViewModel;
            if (graphVm != null && graphVm.TryHandleKeyDown(e))
            {
                e.Handled = true;
            }
        }
    }
}