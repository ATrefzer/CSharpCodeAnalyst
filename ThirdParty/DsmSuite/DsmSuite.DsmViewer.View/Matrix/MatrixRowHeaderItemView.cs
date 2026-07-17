// SPDX-License-Identifier: GPL-3.0-or-later
using DsmSuite.DsmViewer.Model.Interfaces;
using DsmSuite.DsmViewer.ViewModel.Main;
using DsmSuite.DsmViewer.ViewModel.Matrix;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DsmSuite.DsmViewer.View.Matrix
{
    public class MatrixRowHeaderItemView : MatrixFrameworkElement
    {
        private readonly MatrixViewModel _matrixViewModel;
        private readonly MatrixTheme _theme;
        private ElementTreeItemViewModel _viewModel;
        private readonly int _indicatorWidth = 7;

        public MatrixRowHeaderItemView(MatrixViewModel matrixViewModel, MatrixTheme theme)
        {
            _matrixViewModel = matrixViewModel;
            _matrixViewModel.PropertyChanged += OnMatrixViewModelPropertyChanged;
            _theme = theme;

            DataContextChanged += OnDataContextChanged;
        }

        // Removed 2026-07 for CSharpCodeAnalyst: dragging a row header onto another one re-parented the
        // dragged element (OnMouseMove started the drag, OnDrop ran ElementTreeItemViewModel.MoveCommand,
        // which is MatrixViewModel.ChangeElementParentCommand). That edits the DSM model, exactly like the
        // context menus that were removed for the same reason: this is a read-only view onto a parsed code
        // graph, and a re-parented row would no longer say anything about the code. Gone with it:
        // AllowDrop, OnGiveFeedback, OnDragEnter, OnDragLeave, OnDragOver, OnDrop, IsValidDropTarget,
        // GetDropAtIndex, the DataObjectName key, and the IsDropTarget / MoveCommand flags on
        // ElementTreeItemViewModel that had no other reader.

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            _viewModel = e.NewValue as ElementTreeItemViewModel;
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
                ToolTip = _viewModel.ToolTipViewModel;
            }
        }

        public void Redraw()
        {
            InvalidateVisual();
        }

        private void OnMatrixViewModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if ((e.PropertyName == nameof(MatrixViewModel.SelectedRow)) ||
                (e.PropertyName == nameof(MatrixViewModel.HoveredRow)))
            {
                InvalidateVisual();
            }
        }

        private void OnViewModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ElementTreeItemViewModel.Color))
            {
                InvalidateVisual();
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            if ((_viewModel != null) && (ActualWidth > _theme.SpacingWidth) && (ActualHeight > _theme.SpacingWidth))
            {
                bool isHovered = _matrixViewModel.HoveredRow?.Element == _viewModel.Element;
                bool isSelected = _matrixViewModel.SelectedRow?.Element == _viewModel.Element;
                SolidColorBrush background = _theme.GetBackground(_viewModel.Color, isHovered, isSelected);
                Rect backgroundRect = new Rect(1.0, 1.0, ActualWidth - _theme.SpacingWidth, ActualHeight - _theme.SpacingWidth);
                dc.DrawRectangle(background, null, backgroundRect);

                string content = _viewModel.Name;

                if (_viewModel.IsExpanded)
                {
                    Point textLocation = new Point(backgroundRect.X + 8.0, backgroundRect.Y - 20.0);
                    DrawRotatedText(dc, content, textLocation, _theme.TextColor, backgroundRect.Height - 20.0);
                }
                else
                {
                    Rect indicatorRect = new Rect(backgroundRect.Width - _indicatorWidth, 1.0, _indicatorWidth, ActualHeight - _theme.SpacingWidth);

                    //---- Right hand indicator
                    switch (_viewModel.SelectedIndicatorViewMode)
                    {
                        case IndicatorViewMode.Default:
                            {
                                Brush brush = GetIndicatorColor();
                                if (brush != null)
                                    dc.DrawRectangle(brush, null, indicatorRect);
                            }
                            break;
                        case IndicatorViewMode.Search:
                            if (_viewModel.IsMatch)
                                dc.DrawRectangle(_theme.MatrixColorMatch, null, indicatorRect);
                            break;
                        case IndicatorViewMode.Bookmarks:
                            if (_viewModel.IsBookmarked)
                                dc.DrawRectangle(_theme.MatrixColorBookmark, null, indicatorRect);
                            break;
                    }

                    // Removed 2026-07 for CSharpCodeAnalyst: the left hand indicator, an addition of this
                    // fork. Selecting an expanded element marked every leaf beneath it that had a relation
                    // reaching outside the selection, in the same green and blue as the right hand
                    // indicator but with a different meaning, and its real signal was the *absence* of a
                    // bar. Two bars in the same colours saying different things, one of which speaks by not
                    // being there, is more puzzle than help. The flags behind it (IsConsumerIn /
                    // IsProviderIn) went with it, see MatrixViewModel.UpdateRelationFlags.

                    //---- Element Label
                    if (ActualWidth > 70.0)
                    {
                        Point contentTextLocation = new Point(backgroundRect.X + 20.0, backgroundRect.Y + 16.0);
                        DrawText(dc, content, contentTextLocation, _theme.TextColor, ActualWidth - 70.0);
                    }

                    //---- Element number
                    string order = _viewModel.Order.ToString();
                    double textWidth = MeasureText(order);

                    Point orderTextLocation = new Point(backgroundRect.X - 25.0 + backgroundRect.Width - textWidth, backgroundRect.Y + 16.0);
                    if (orderTextLocation.X > 0)
                    {
                        DrawText(dc, order, orderTextLocation, _theme.TextColor, ActualWidth - 25.0);
                    }
                }

                Point expanderLocation = new Point(backgroundRect.X + 1.0, backgroundRect.Y + 1.0);
                DrawExpander(dc, expanderLocation);
            }
        }

        /// <summary>
        /// Return the brush to use for the right hand consumer/producer indicator in the row,
        /// or null if this element is neither producer nor consumer.
        /// </summary>
        private Brush? GetIndicatorColor()
        {
            Brush brush = null;
            if (_viewModel.IsConsumer  &&  _viewModel.IsProvider)
                brush = _theme.MatrixColorCycle;
            else if (_viewModel.IsConsumer)
                brush = _theme.MatrixColorConsumer;
            else if (_viewModel.IsProvider)
                brush = _theme.MatrixColorProvider;

            return brush;
        }


        private void DrawExpander(DrawingContext dc, Point location)
        {
            if (_viewModel.IsExpandable)
            {
                dc.DrawText(
                    _viewModel.IsExpanded
                        ? _theme.DownArrowFormattedText
                        : _theme.RightArrowFormattedText, location);
            }
        }
    }
}