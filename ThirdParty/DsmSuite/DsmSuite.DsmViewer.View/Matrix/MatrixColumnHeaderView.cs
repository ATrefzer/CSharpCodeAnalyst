// SPDX-License-Identifier: GPL-3.0-or-later
using DsmSuite.DsmViewer.ViewModel.Matrix;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DsmSuite.DsmViewer.View.Matrix
{
    public class MatrixColumnHeaderView : MatrixFrameworkElement
    {
        /// <summary>Added 2026-07 for CSharpCodeAnalyst: distance from the top of the header to the first
        /// character of a column label, see OnRender.</summary>
        private const double TextTopMargin = 6.0;

        /// <summary>Added 2026-07 for CSharpCodeAnalyst: gap between the element order and the name.</summary>
        private const double OrderNameGap = 8.0;

        /// <summary>
        /// Added 2026-07 for CSharpCodeAnalyst: the collapsed header (names off) reserves vertical room for
        /// at least this many digits of element order. Element order runs to the size of the whole tree, so
        /// four digits (1000+ elements) is a normal case; reserving for it keeps a four digit number from
        /// being cramped and keeps the header height steady when bigger orders scroll or expand into view.
        /// The header still grows past this if an order is ever wider.
        /// </summary>
        private const int MinCollapsedOrderDigits = 4;

        private MatrixViewModel _viewModel;
        private readonly MatrixTheme _theme;
        private Rect _rect;
        private int? _hoveredColumn;
        private readonly double _pitch;
        private readonly double _offset;

        public MatrixColumnHeaderView()
        {
            _theme = new MatrixTheme(this);
            _rect = new Rect(new Size(_theme.MatrixCellSize, _theme.MatrixHeaderHeight));
            _hoveredColumn = null;
            _pitch = _theme.MatrixCellSize + _theme.SpacingWidth;
            _offset = _theme.SpacingWidth / 2;

            DataContextChanged += OnDataContextChanged;
            MouseMove += OnMouseMove;
            MouseDown += OnMouseDown;
            MouseLeave += OnMouseLeave;
        }

        /// <summary>
        /// Added 2026-07 for CSharpCodeAnalyst: width of the column the element order is right aligned in,
        /// taken from the longest order on screen so that every name starts at the same offset. Element
        /// order runs from 1 to the number of elements in the whole tree, so it is regularly four digits
        /// and up, and the widths do differ.
        /// </summary>
        private double MeasureOrderFieldWidth(int matrixSize)
        {
            double widest = 0.0;
            for (int column = 0; column < matrixSize; column++)
            {
                double width = MeasureText(_viewModel.ColumnElementIds[column].ToString());
                if (width > widest)
                {
                    widest = width;
                }
            }

            return widest;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Unsubscribe from the previous view model, see MatrixCellsView.OnDataContextChanged for the
            // leak and the crash it causes. Same defect here, reached by hovering the column headers.
            if (e.OldValue is MatrixViewModel oldViewModel)
            {
                oldViewModel.PropertyChanged -= OnPropertyChanged;
            }

            _viewModel = DataContext as MatrixViewModel;
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += OnPropertyChanged;
                InvalidateVisual();
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            int column = GetHoveredColumn(e.GetPosition(this));
            if (_hoveredColumn != column)
            {
                _hoveredColumn = column;
                _viewModel.HoverColumn(column);
            }
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            _viewModel.HoverColumn(null);
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            int column = GetHoveredColumn(e.GetPosition(this));
            _viewModel.SelectColumn(column);
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // A stale subscription can still deliver here after the DataContext is gone, see
            // MatrixCellsView.OnPropertyChanged.
            if (_viewModel == null)
            {
                return;
            }

            if (e.PropertyName == nameof(MatrixViewModel.ColumnHeaderToolTipViewModel))
            {
                ToolTip = _viewModel.ColumnHeaderToolTipViewModel;
            }

            // Changed 2026-07 for CSharpCodeAnalyst: HoveredColumn no longer invalidates the header. Hovering
            // a cell changes the hovered column on every mouse move, and redrawing the header (matrixSize
            // rotated glyph runs, re-rasterised at the current zoom) on each of those was a big part of what
            // made hovering the matrix crawl. The crosshair overlay already marks the hovered column in the
            // cells; the header no longer highlights it. Selection stays - it is click-driven and rare.
            if ((e.PropertyName == nameof(MatrixViewModel.MatrixSize)) ||
                (e.PropertyName == nameof(MatrixViewModel.SelectedColumn)) ||
                // Added 2026-07 for CSharpCodeAnalyst: the toggle changes what OnRender draws and how tall
                // the header is, so it has to redraw.
                (e.PropertyName == nameof(MatrixViewModel.ColumnNamesVisible)))
            {
                InvalidateVisual();
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (_viewModel != null)
            {
                int matrixSize = _viewModel.MatrixSize;
                double orderFieldWidth = MeasureOrderFieldWidth(matrixSize);

                // Added 2026-07 for CSharpCodeAnalyst: the toolbar toggle. With names off the header only
                // has to hold the rotated order number, so it collapses to the order field plus a margin at
                // each end instead of the full MatrixHeaderHeight - that reclaimed band is the whole point
                // of the toggle. Anchoring is unchanged (top), so the number stays exactly where it was.
                bool namesVisible = _viewModel.ColumnNamesVisible;
                double collapsedOrderField = Math.Max(orderFieldWidth,
                    MeasureText(new string('9', MinCollapsedOrderDigits)));
                double headerHeight = namesVisible
                    ? _theme.MatrixHeaderHeight
                    : TextTopMargin + collapsedOrderField + TextTopMargin;
                _rect.Height = headerHeight;

                for (int column = 0; column < matrixSize; column++)
                {
                    _rect.X = _offset + column * _pitch;
                    _rect.Y = 0;

                    // Changed 2026-07 for CSharpCodeAnalyst: no hover highlight in the header anymore, see
                    // OnPropertyChanged. The hovered column is marked by the crosshair overlay over the cells.
                    bool isSelected = column == _viewModel.SelectedColumn?.Index;
                    MatrixColor color = _viewModel.ColumnColors[column];
                    SolidColorBrush background = _theme.GetBackground(color, false, isSelected);

                    dc.DrawRectangle(background, null, _rect);

                    // Changed 2026-07 for CSharpCodeAnalyst: this used to draw the element order alone,
                    // anchored at the bottom of the header. The order made every column a lookup into the
                    // row headers, and the anchoring was broken for anything longer: the draw origin was
                    //     MatrixHeaderHeight - 10 - textWidth
                    // so a label as wide as the header started above y=0 and lost its leading characters,
                    // while DrawText clipped the tail at maxWidth at the same time. Text was cut off at
                    // both ends. It never showed upstream, where the header only ever held a short number
                    // that always fit.
                    //
                    // Order and name are now drawn as two columns, the way a numbered list is set: the
                    // order right aligned in a field as wide as the longest one, the name always starting
                    // at the same offset. Anchoring at the top is what makes an overlong name lose its
                    // tail instead of its head, and drawing the two separately is what keeps the names
                    // aligned even though the order is variable width (1 vs 1234).
                    string order = _viewModel.ColumnElementIds[column].ToString();

                    double orderStart = TextTopMargin + orderFieldWidth - MeasureText(order);
                    DrawRotatedText(dc, order, new Point(_rect.X + 10.0, -orderStart), _theme.TextColor,
                        orderFieldWidth);

                    // Changed 2026-07 for CSharpCodeAnalyst: ellipsized, see Ellipsize. Without it a name
                    // longer than the header was simply cut, so two types whose names share a prefix ended
                    // up with identical column labels.
                    // Changed 2026-07 for CSharpCodeAnalyst: skipped when the toolbar toggle hides names.
                    if (namesVisible)
                    {
                        string name = _viewModel.ColumnElementNames[column];
                        double nameStart = TextTopMargin + orderFieldWidth + OrderNameGap;
                        double nameBudget = _theme.MatrixHeaderHeight - nameStart - _theme.SpacingWidth;
                        DrawRotatedText(dc, Ellipsize(name, nameBudget), new Point(_rect.X + 10.0, -nameStart),
                            _theme.TextColor, nameBudget);
                    }
                }

                // Changed 2026-07 for CSharpCodeAnalyst: follows the toggle, see headerHeight above. The
                // hosting Canvas in MatrixView.xaml binds its Height to this ActualHeight, so shrinking here
                // is what actually collapses the header band.
                Height = headerHeight + _theme.SpacingWidth;
                Width = _theme.MatrixCellSize * matrixSize + _theme.SpacingWidth;
            }
        }

        private int GetHoveredColumn(Point location)
        {
            double column = (location.X - _offset) / _pitch;
            return (int)column;
        }
    }

}
