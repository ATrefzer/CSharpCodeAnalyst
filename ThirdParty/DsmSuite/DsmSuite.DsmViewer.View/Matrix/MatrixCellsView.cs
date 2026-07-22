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

        /// <summary>
        /// Added 2026-07 for CSharpCodeAnalyst: smallest on screen font size at which a cell weight is
        /// still worth drawing, in device pixels. Below it the cells switch to presence, see
        /// <see cref="DrawWeightsAtCurrentZoom"/>. At CellFontSize 10 this puts the switch at zoom 0.7.
        /// </summary>
        private const double MinReadableFontSize = 7.0;

        /// <summary>
        /// Added 2026-07 for CSharpCodeAnalyst: what the last OnRender decided about the weights. Null
        /// until the first render, which is why a zoom before that always redraws once.
        /// </summary>
        private bool? _weightsDrawn;

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
            // Unsubscribe from the previous view model. Without this a view that is discarded while its view
            // model lives on (e.g. a host that keeps one view model but rebuilds this view's visual tree)
            // stays subscribed and reachable from the view model, so it is neither collected nor silent: its
            // DataContext is gone, so _viewModel is null, and the next property change reaches OnPropertyChanged,
            // which dereferences _viewModel and throws.
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
            // A stale subscription can still deliver here after the DataContext is gone; every other member
            // guards against a null view model, OnRender and the mouse handlers included, so match them.
            if (_viewModel == null)
            {
                return;
            }

            if (e.PropertyName == nameof(MatrixViewModel.CellToolTipViewModel))
            {
                ToolTip = _viewModel.CellToolTipViewModel;
            }

            // Added 2026-07 for CSharpCodeAnalyst: zooming reaches OnRender, but only where it changes the
            // outcome. The LayoutTransform above us scales the visual we already produced, so the zoom
            // enters the drawing through exactly one boolean - see DrawWeightsAtCurrentZoom. Redrawing on
            // every zoom step instead froze the application: OnRender walks matrixSize squared cells, one
            // wheel spin is a dozen steps, and each of them rebuilt every cell of the matrix for a picture
            // that had not changed.
            if (e.PropertyName == nameof(MatrixViewModel.ZoomLevel))
            {
                if (DrawWeightsAtCurrentZoom() != _weightsDrawn)
                {
                    InvalidateVisual();
                }

                return;
            }

            // Changed 2026-07 for CSharpCodeAnalyst: hover and selection no longer invalidate the cells -
            // the crosshair moved to MatrixCrosshairView, which redraws itself instead. That is the whole
            // point of the overlay: a mouse move must not force a re-raster of the scaled matrix. Only a
            // size change still rebuilds the cells.
            if (e.PropertyName == nameof(MatrixViewModel.MatrixSize))
            {
                InvalidateVisual();
            }
        }

        /// <summary>
        /// Added 2026-07 for CSharpCodeAnalyst: a bounds-only hit test. The cells render matrixSize² filled
        /// rectangles into one drawing, and WPF's default geometry hit test walks them on every mouse move
        /// (MilUtility_PolygonHitTest / HitTestFiguresFill in the profiler). We only need to know the point
        /// is over the view; the row and column are derived arithmetically in OnMouseMove. This is O(1).
        /// </summary>
        protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        {
            return new Rect(RenderSize).Contains(hitTestParameters.HitPoint)
                ? new PointHitTestResult(this, hitTestParameters.HitPoint)
                : null;
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (_viewModel != null)
            {
                // Removed 2026-07 for CSharpCodeAnalyst: weightBrush and weightRect, only the weight bar
                // used them.

                // Added 2026-07 for CSharpCodeAnalyst: below this the cells are painted by presence
                // instead, see DrawWeightsAtCurrentZoom. Remembered so that a zoom step which does not
                // cross the threshold costs nothing, see OnPropertyChanged.
                bool drawWeights = DrawWeightsAtCurrentZoom();
                _weightsDrawn = drawWeights;

                int matrixSize = _viewModel.MatrixSize;
                for (int row = 0; row < matrixSize; row++)
                {
                    for (int column = 0; column < matrixSize; column++)
                    {
                        _rect.X = _offset + column * _pitch;
                        _rect.Y = _offset + row * _pitch;

                        MatrixColor color = _viewModel.CellColors[row][column];
                        int weight = _viewModel.CellWeights[row][column];

                        // Added 2026-07 for CSharpCodeAnalyst: once the weight is too small to read, a
                        // populated cell says so by being filled instead. Cycles keep their own colour,
                        // which outranks both.
                        //
                        // Changed 2026-07 for CSharpCodeAnalyst: the cells no longer carry the hover /
                        // selection highlight - it is a translucent overlay now (MatrixCrosshairView), so a
                        // mouse move does not re-render (and the composition thread does not re-rasterise)
                        // the whole scaled matrix. Hence the fixed false / false here.
                        bool paintPresence = !drawWeights && (weight > 0) && (color != MatrixColor.Cycle);
                        SolidColorBrush background = paintPresence
                            ? _theme.GetPresenceBackground(false, false)
                            : _theme.GetBackground(color, false, false);

                        dc.DrawRectangle(background, null, _rect);

                        if (drawWeights && (weight > 0))
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

        /// <summary>
        /// Added 2026-07 for CSharpCodeAnalyst: whether the weight can still be read at the current zoom.
        /// </summary>
        /// <remarks>
        /// The zoom is a LayoutTransform on the grid above us, so it scales the cell and its number by the
        /// same factor: the number always fits, it just gets smaller. Fit is therefore not a criterion that
        /// can ever fire, and nothing in this view's own geometry changes when zooming - the only thing
        /// that tells us the matrix has become unreadable is the zoom level itself.
        /// <para>
        /// Below the threshold the number is dropped and the cell is painted by presence instead. It is a
        /// hard switch because there is nothing gradual to be had: the last legible size is followed by an
        /// illegible one. Leaving the number in was worse than dropping it - an unreadable glyph still
        /// tints its cell, so a populated cell read as a slightly different shade rather than as full,
        /// which is the one thing you are looking for at this zoom.
        /// </para>
        /// </remarks>
        private bool DrawWeightsAtCurrentZoom()
        {
            return _viewModel.ZoomLevel * CellFontSize >= MinReadableFontSize;
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
