// SPDX-License-Identifier: GPL-3.0-or-later
// Added 2026-07 for CSharpCodeAnalyst: whole file. See the class summary.
using DsmSuite.DsmViewer.ViewModel.Matrix;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DsmSuite.DsmViewer.View.Matrix
{
    /// <summary>
    /// Added 2026-07 for CSharpCodeAnalyst: the hover / selection crosshair, as translucent bands over the
    /// cells instead of baked into every cell.
    /// </summary>
    /// <remarks>
    /// Two stages. First the crosshair was pulled out of <see cref="MatrixCellsView"/> - upstream asked
    /// <c>GetBackground(color, isHovered, isSelected)</c> per cell, so one mouse move invalidated the whole
    /// cells view and the composition thread re-rasterised the entire zoom-scaled matrix (a dotTrace
    /// timeline at high zoom was 57% <c>MilComposition_SyncFlush</c>, the cells' own <c>OnRender</c> under
    /// 3%). Drawing four rectangles in an overlay's <c>OnRender</c> instead did not help, though: the
    /// overlay is a matrix-sized, translucent element, and invalidating it per hover made the composition
    /// thread re-composite that whole huge layer - still 61% SyncFlush.
    /// <para>
    /// So the bands are fixed <see cref="Rectangle"/>s now, moved by a <see cref="TranslateTransform"/>.
    /// A hover change only sets a transform offset, which the composition thread applies to a cached band -
    /// no <c>OnRender</c>, and only the band's old and new region is re-composited, not the whole matrix.
    /// Sizes change only when the matrix does (expand / collapse), which is rare. The element is not hit
    /// test visible, so the cells still receive the mouse.
    /// </para>
    /// </remarks>
    public class MatrixCrosshairView : Canvas
    {
        private MatrixViewModel _viewModel;
        private readonly MatrixTheme _theme;
        private readonly double _pitch;
        private readonly double _offset;

        // Translucent black; the alpha darkens a band about as much as the old per-cell highlight did (which
        // subtracted 26 / 45 channel steps). Where a row and column band cross, the alpha stacks, so the
        // crossing stays the darkest point - as it was before.
        private static readonly SolidColorBrush HoverBrush = Frozen(Color.FromArgb(28, 0, 0, 0));
        private static readonly SolidColorBrush SelectedBrush = Frozen(Color.FromArgb(52, 0, 0, 0));

        private readonly Rectangle _hoverColumn;
        private readonly Rectangle _hoverRow;
        private readonly Rectangle _selectedColumn;
        private readonly Rectangle _selectedRow;
        private readonly TranslateTransform _hoverColumnPos = new TranslateTransform();
        private readonly TranslateTransform _hoverRowPos = new TranslateTransform();
        private readonly TranslateTransform _selectedColumnPos = new TranslateTransform();
        private readonly TranslateTransform _selectedRowPos = new TranslateTransform();

        public MatrixCrosshairView()
        {
            _theme = new MatrixTheme(this);
            _pitch = _theme.MatrixCellSize + _theme.SpacingWidth;
            _offset = _theme.SpacingWidth / 2;
            IsHitTestVisible = false;

            _hoverColumn = AddBand(HoverBrush, _hoverColumnPos);
            _hoverRow = AddBand(HoverBrush, _hoverRowPos);
            _selectedColumn = AddBand(SelectedBrush, _selectedColumnPos);
            _selectedRow = AddBand(SelectedBrush, _selectedRowPos);

            DataContextChanged += OnDataContextChanged;
        }

        private Rectangle AddBand(Brush fill, Transform position)
        {
            var band = new Rectangle
            {
                Fill = fill,
                Visibility = Visibility.Collapsed,
                RenderTransform = position
            };
            Children.Add(band);
            return band;
        }

        private static SolidColorBrush Frozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        /// <summary>Unsubscribe from the previous view model, see <see cref="MatrixCellsView"/>.</summary>
        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is MatrixViewModel oldViewModel)
            {
                oldViewModel.PropertyChanged -= OnPropertyChanged;
            }

            _viewModel = DataContext as MatrixViewModel;
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += OnPropertyChanged;
                ResizeBands();
                UpdateAll();
            }
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_viewModel == null)
            {
                return;
            }

            switch (e.PropertyName)
            {
                case nameof(MatrixViewModel.MatrixSize):
                    ResizeBands();
                    UpdateAll();
                    break;
                case nameof(MatrixViewModel.HoveredColumn):
                    PlaceColumn(_hoverColumn, _hoverColumnPos, _viewModel.HoveredColumn?.Index);
                    break;
                case nameof(MatrixViewModel.HoveredRow):
                    PlaceRow(_hoverRow, _hoverRowPos, _viewModel.HoveredRow?.Index);
                    break;
                case nameof(MatrixViewModel.SelectedColumn):
                    PlaceColumn(_selectedColumn, _selectedColumnPos, _viewModel.SelectedColumn?.Index);
                    break;
                case nameof(MatrixViewModel.SelectedRow):
                    PlaceRow(_selectedRow, _selectedRowPos, _viewModel.SelectedRow?.Index);
                    break;
            }
        }

        private void UpdateAll()
        {
            PlaceColumn(_hoverColumn, _hoverColumnPos, _viewModel.HoveredColumn?.Index);
            PlaceRow(_hoverRow, _hoverRowPos, _viewModel.HoveredRow?.Index);
            PlaceColumn(_selectedColumn, _selectedColumnPos, _viewModel.SelectedColumn?.Index);
            PlaceRow(_selectedRow, _selectedRowPos, _viewModel.SelectedRow?.Index);
        }

        /// <summary>
        /// Sizes the bands to the matrix. Only the length (the whole matrix) depends on the size; the
        /// thickness is one cell. Called when the matrix is (re)built, not on hover.
        /// </summary>
        private void ResizeBands()
        {
            double full = _pitch * _viewModel.MatrixSize;
            double cell = _theme.MatrixCellSize;

            _hoverColumn.Width = _selectedColumn.Width = cell;
            _hoverColumn.Height = _selectedColumn.Height = full;
            _hoverRow.Height = _selectedRow.Height = cell;
            _hoverRow.Width = _selectedRow.Width = full;

            Width = Height = full;
        }

        private void PlaceColumn(Rectangle band, TranslateTransform position, int? index)
        {
            if (index is int i && i >= 0 && i < _viewModel.MatrixSize)
            {
                position.X = _offset + i * _pitch;
                position.Y = 0;
                band.Visibility = Visibility.Visible;
            }
            else
            {
                band.Visibility = Visibility.Collapsed;
            }
        }

        private void PlaceRow(Rectangle band, TranslateTransform position, int? index)
        {
            if (index is int i && i >= 0 && i < _viewModel.MatrixSize)
            {
                position.X = 0;
                position.Y = _offset + i * _pitch;
                band.Visibility = Visibility.Visible;
            }
            else
            {
                band.Visibility = Visibility.Collapsed;
            }
        }
    }
}
