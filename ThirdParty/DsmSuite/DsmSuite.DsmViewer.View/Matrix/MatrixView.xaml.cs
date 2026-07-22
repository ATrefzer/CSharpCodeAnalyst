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
        /// Added 2026-07 for CSharpCodeAnalyst: how much of the visible area one wheel notch scrolls. Tying
        /// the step to the viewport rather than to a fixed content distance makes it feel the same at every
        /// zoom. The scroll offset is in the matrix's pre-zoom coordinates (the ScaleTransform is a
        /// LayoutTransform above the ScrollViewer) and the viewport shrinks as the zoom grows, so a fixed
        /// content step moved almost nothing on screen when zoomed out — a couple of rows out of hundreds —
        /// and a screenful when zoomed in. A fraction of the viewport is the same fraction of the screen at
        /// any zoom, because viewport × zoom is the on-screen size.
        /// </summary>
        private const double WheelScrollViewportFraction = 0.15;

        // Handle the wheel ourselves so scrolling works from anywhere in the matrix, headers included. Plain
        // wheel scrolls vertically, shift+wheel horizontally.
        //
        // Changed 2026-07 for CSharpCodeAnalyst: shift+wheel scrolls sideways (a plain ScrollViewer only
        // handles the wheel vertically, and the horizontal bar sits inside the scaled grid, so zooming out
        // shrinks it until it cannot be grabbed — this makes it unnecessary). The vertical axis is explicit
        // too, and both steps are a fraction of the viewport, see WheelScrollViewportFraction.
        private void HandlePreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }

            ScrollViewer cells = (ScrollViewer) FindName("ScrolledCellsView");
            int direction = Math.Sign(e.Delta);
            e.Handled = true;

            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                cells.ScrollToHorizontalOffset(
                    cells.HorizontalOffset - direction * WheelScrollViewportFraction * cells.ViewportWidth);
                return;
            }

            cells.ScrollToVerticalOffset(
                cells.VerticalOffset - direction * WheelScrollViewportFraction * cells.ViewportHeight);
        }
    }
}
