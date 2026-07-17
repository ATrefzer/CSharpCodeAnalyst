// SPDX-License-Identifier: GPL-3.0-or-later
using DsmSuite.DsmViewer.ViewModel.Matrix;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DsmSuite.DsmViewer.View.Matrix
{
    /// <summary>
    /// The view for the square block of cells in a matrix.
    /// </summary>
    public class MatrixCellsView : MatrixFrameworkElement
    {
        private MatrixViewModel _viewModel;
        private readonly MatrixTheme _theme;
        private Rect _rect;     // Area of the cell that is being rendered (reused)
        private int? _hoveredRow;
        private int? _hoveredColumn;
        private readonly double _pitch;     // Distance between the same points in neighbouring cells
        private readonly double _offset;    // Distance between header and first cell (hor/ver)
        private readonly double _verticalTextOffset; // Distance between top of cell and baseline of text

        /// <summary>
        /// Added 2026-07 for CSharpCodeAnalyst: the weight is drawn smaller than the rest of the matrix.
        /// </summary>
        /// <remarks>
        /// At the shared font size of 14 a digit is 7.55px wide, so only three of them fit the 22px a cell
        /// leaves. DrawText tests the width *before* each glyph, so a fourth digit was silently dropped
        /// rather than overflowing: 1000 was drawn as "100" and 9999 as "999" — a wrong number, with
        /// nothing to hint at it. At 10 the four digits take 21.6px, and three digits get 3.9px of air
        /// instead of 0.7px, which is what made them look like they leaked into the neighbouring cell.
        /// </remarks>
        private const double CellFontSize = 10.0;

        /// <summary>
        /// Added 2026-07 for CSharpCodeAnalyst: above this the weight does not fit and is labelled
        /// <see cref="TooLargeLabel"/>.
        /// </summary>
        private const int LargestDrawableWeight = 9999;

        /// <summary>
        /// Added 2026-07 for CSharpCodeAnalyst: replaces the upstream infinity sign, which claimed a weight
        /// was infinite when it only meant it did not fit. This states what is actually known, and names
        /// the bound instead of leaving the reader to guess it. 18.0px at CellFontSize, so it fits.
        /// </summary>
        private const string TooLargeLabel = ">9K";

        public MatrixCellsView()
        {
            _theme = new MatrixTheme(this);
            _rect = new Rect(new Size(_theme.MatrixCellSize, _theme.MatrixCellSize));
            _hoveredRow = null;
            _hoveredColumn = null;
            _pitch = _theme.MatrixCellSize + _theme.SpacingWidth;
            _offset = _theme.SpacingWidth / 2;

            // Changed 2026-07 for CSharpCodeAnalyst: was a fixed 12.0, which put the baseline exactly on
            // the middle of the cell so that the number sat in the upper half, above the weight bar. The
            // bar is gone, so the number is centred instead. Horizontally it already was: _offset plus
            // half a cell equals half a pitch.
            _verticalTextOffset = _offset + CenteredTextBaseline(_theme.MatrixCellSize, CellFontSize);

            DataContextChanged += OnDataContextChanged;
            MouseMove += OnMouseMove;
            MouseDown += OnMouseDown;
            MouseLeave += OnMouseLeave;
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
            int row = GetHoveredRow(e.GetPosition(this));
            int column = GetHoveredColumn(e.GetPosition(this));
            if ((_hoveredRow != row) || (_hoveredColumn != column))
            {
                _hoveredRow = row;
                _hoveredColumn = column;
                _viewModel.HoverCell(row, column);
            }
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            _viewModel.HoverCell(null, null);
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            int row = GetHoveredRow(e.GetPosition(this));
            int column = GetHoveredColumn(e.GetPosition(this));
            _viewModel.SelectCell(row, column);
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MatrixViewModel.CellToolTipViewModel))
            {
                ToolTip = _viewModel.CellToolTipViewModel;
            }

            if ((e.PropertyName == nameof(MatrixViewModel.MatrixSize)) ||
                (e.PropertyName == nameof(MatrixViewModel.HoveredRow)) ||
                (e.PropertyName == nameof(MatrixViewModel.SelectedRow)) ||
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
                // Removed 2026-07 for CSharpCodeAnalyst: weightBrush and weightRect, only the weight bar
                // used them.
                int matrixSize = _viewModel.MatrixSize;
                for (int row = 0; row < matrixSize; row++)
                {
                    for (int column = 0; column < matrixSize; column++)
                    {
                        _rect.X = _offset + column * _pitch;
                        _rect.Y = _offset + row * _pitch;

                        bool isHovered = row == _viewModel.HoveredRow?.Index  ||  column == _viewModel.HoveredColumn?.Index;
                        bool isSelected = row == _viewModel.SelectedRow?.Index  ||  column == _viewModel.SelectedColumn?.Index;
                        MatrixColor color = _viewModel.CellColors[row][column];
                        SolidColorBrush background = _theme.GetBackground(color, isHovered, isSelected);

                        dc.DrawRectangle(background, null, _rect);

                        int weight = _viewModel.CellWeights[row][column];
                        if (weight > 0)
                        {
                            // Removed 2026-07 for CSharpCodeAnalyst: the weight was also drawn as a small
                            // filled bar across the lower half of the cell, its width the weight's decile
                            // among all populated cells. The number states it already, and the bar was
                            // misleading more often than not: the deciles all collapse onto one bucket
                            // when fewer than ten cells are populated, and with the tree fully expanded
                            // every weight is 1 (we feed one deduplicated edge per pair of types), so
                            // every bar came out the same length regardless.

                            //---- Weight as a number
                            // Changed 2026-07 for CSharpCodeAnalyst: was the infinity sign above 9999, see
                            // TooLargeLabel, and drawn at the shared font size, see CellFontSize.
                            string content = weight > LargestDrawableWeight ? TooLargeLabel : weight.ToString();

                            double textWidth = MeasureText(content, CellFontSize);

                            Point location = new Point
                            {
                                X = (column * _pitch) + (_pitch - textWidth) / 2,
                                Y = (row * _pitch) + _verticalTextOffset
                            };
                            DrawText(dc, content, location, _theme.TextColor, _rect.Width - _theme.SpacingWidth, CellFontSize);
                        }
                    }
                }
                Height = Width = _pitch * matrixSize;
            }
        }

        private int GetHoveredRow(Point location)
        {
            double row = (location.Y - _offset) / _pitch;
            return (int)row;
        }

        private int GetHoveredColumn(Point location)
        {
            double column = (location.X - _offset) / _pitch;
            return (int)column;
        }
    }
}
