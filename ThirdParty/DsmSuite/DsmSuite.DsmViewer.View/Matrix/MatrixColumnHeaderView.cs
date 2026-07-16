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
            if (e.PropertyName == nameof(MatrixViewModel.ColumnHeaderToolTipViewModel))
            {
                ToolTip = _viewModel.ColumnHeaderToolTipViewModel;
            }

            if ((e.PropertyName == nameof(MatrixViewModel.MatrixSize)) ||
                (e.PropertyName == nameof(MatrixViewModel.HoveredColumn)) ||
                (e.PropertyName == nameof(MatrixViewModel.SelectedColumn)))
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
                for (int column = 0; column < matrixSize; column++)
                {
                    _rect.X = _offset + column * _pitch;
                    _rect.Y = 0;

                    bool isHovered = column == _viewModel.HoveredColumn?.Index;
                    bool isSelected = column == _viewModel.SelectedColumn?.Index;
                    MatrixColor color = _viewModel.ColumnColors[column];
                    SolidColorBrush background = _theme.GetBackground(color, isHovered, isSelected);

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
                    string name = _viewModel.ColumnElementNames[column];

                    double orderStart = TextTopMargin + orderFieldWidth - MeasureText(order);
                    DrawRotatedText(dc, order, new Point(_rect.X + 10.0, -orderStart), _theme.TextColor,
                        orderFieldWidth);

                    double nameStart = TextTopMargin + orderFieldWidth + OrderNameGap;
                    DrawRotatedText(dc, name, new Point(_rect.X + 10.0, -nameStart), _theme.TextColor,
                        _theme.MatrixHeaderHeight - nameStart - _theme.SpacingWidth);
                }

                Height = _theme.MatrixHeaderHeight + _theme.SpacingWidth;
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
