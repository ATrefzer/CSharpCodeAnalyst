using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using System.Windows.Input;
using System.Windows.Threading;
using CSharpCodeAnalyst.Configuration;
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

    public void SetViewer(GraphViewState graphViewState, IPublisher publisher, ISubscriber subscriber, AppSettings settings)
    {
        // The web view observes the shared model directly and listens on the bus for
        // render-only commands (Layout / Refit / Export). The graph search box (bound to
        // MainViewModel.GraphSearchViewModel) lives in the web tab's tool bar.
        WebGraphView.SetViewer(graphViewState, publisher, subscriber, settings);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Tear down the WebView2 (and its browser processes) cleanly on shutdown.
        // Dispose() stops it internally and is null-safe via ?. .
        WebGraphView?.WebView?.Dispose();
        base.OnClosing(e);
    }

    private void WebSearchBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // Focus the box (and select its text) when the search row slides open.
        if (sender is TextBox { IsVisible: true } box)
        {
            box.Dispatcher.BeginInvoke(() =>
            {
                box.Focus();
                box.SelectAll();
            });
        }
    }

    private void WebSearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Esc closes the search row (the view model clears the search on hide).
        if (e.Key == Key.Escape && sender is FrameworkElement { DataContext: GraphSearchViewModel vm })
        {
            vm.IsSearchVisible = false;
            e.Handled = true;
        }
    }
}