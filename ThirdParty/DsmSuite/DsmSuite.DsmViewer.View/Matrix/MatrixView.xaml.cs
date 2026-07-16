// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DsmSuite.DsmViewer.View.Matrix
{
    /// <summary>
    /// Interaction logic for MatrixView.xaml
    /// </summary>
    public partial class MatrixView
    {
        public MatrixView()
        {
            InitializeComponent();
            PreviewMouseWheel += HandlePreviewMouseWheel;
        }

        public double UsedWidth => RowHeaderView.ActualWidth + Splitter.ActualWidth + MatrixMetricsSelectorView.ActualWidth + Math.Min(CellsView.ActualWidth, ScrolledCellsView.ActualWidth);

        public double UsedHeight => ColumnHeaderView.ActualHeight + Math.Min(CellsView.ActualHeight, ScrolledCellsView.ActualHeight);

        private void CellsViewOnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            Canvas.SetLeft(ColumnHeaderView, -e.HorizontalOffset);
            Canvas.SetTop(RowHeaderView, -e.VerticalOffset);
            //Canvas.SetTop(IndicatorView, -e.VerticalOffset);
            Canvas.SetTop(RowMetricsView, -e.VerticalOffset);
        }

        // Pass mouse wheel events on to the ScrollViewer, so that the user can scroll using
        // the wheel even when the mouse cursor is not over the matrix cells, but on the headers.
        private void HandlePreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
                eventArg.RoutedEvent = UIElement.MouseWheelEvent;
                eventArg.Source = sender;
                ScrollViewer cells = (ScrollViewer) FindName("ScrolledCellsView");
                eventArg.Source = cells;
                cells.RaiseEvent(eventArg);
            }
        }
    }
}
