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

        /// <summary>
        /// Added 2026-07 for CSharpCodeAnalyst: how far shift+wheel scrolls sideways per notch. Matches
        /// what a ScrollViewer does vertically for one notch: three lines of 16.
        /// </summary>
        private const double HorizontalWheelScrollAmount = 48.0;

        // Pass mouse wheel events on to the ScrollViewer, so that the user can scroll using
        // the wheel even when the mouse cursor is not over the matrix cells, but on the headers.
        private void HandlePreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }

            ScrollViewer cells = (ScrollViewer) FindName("ScrolledCellsView");

            // Added 2026-07 for CSharpCodeAnalyst: shift+wheel scrolls sideways. A plain ScrollViewer only
            // handles the wheel vertically, so the horizontal scroll bar was the only way across — and that
            // bar sits inside the scaled grid, so zooming out shrinks it until it cannot be grabbed. This
            // makes it unnecessary rather than fixing its width.
            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                e.Handled = true;
                cells.ScrollToHorizontalOffset(
                    cells.HorizontalOffset - Math.Sign(e.Delta) * HorizontalWheelScrollAmount);
                return;
            }

            e.Handled = true;
            var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
            eventArg.RoutedEvent = UIElement.MouseWheelEvent;
            eventArg.Source = sender;
            eventArg.Source = cells;
            cells.RaiseEvent(eventArg);
        }
    }
}
