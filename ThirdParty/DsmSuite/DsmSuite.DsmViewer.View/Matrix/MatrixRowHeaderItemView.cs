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
        private static readonly string DataObjectName = "Element";
        private readonly MatrixTheme _theme;
        private ElementTreeItemViewModel _viewModel;
        private readonly int _indicatorWidth = 7;

        public MatrixRowHeaderItemView(MatrixViewModel matrixViewModel, MatrixTheme theme)
        {
            _matrixViewModel = matrixViewModel;
            _matrixViewModel.PropertyChanged += OnMatrixViewModelPropertyChanged;
            _theme = theme;

            AllowDrop = true;

            DataContextChanged += OnDataContextChanged;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DataObject data = new DataObject();
                data.SetData(DataObjectName, _viewModel);
                DragDrop.DoDragDrop(this, data, DragDropEffects.Move);
            }
        }

        protected override void OnGiveFeedback(GiveFeedbackEventArgs e)
        {
            base.OnGiveFeedback(e);

            Mouse.SetCursor(e.Effects.HasFlag(DragDropEffects.Move) ? Cursors.Pen : Cursors.Arrow);
            e.Handled = true;
        }

        protected override void OnDragEnter(DragEventArgs e)
        {
            if (IsValidDropTarget(e))
            {
                _viewModel.IsDropTarget = true;
            }
        }

        protected override void OnDragLeave(DragEventArgs e)
        {
            _viewModel.IsDropTarget = false;
        }

        protected override void OnDragOver(DragEventArgs e)
        {
            base.OnDragOver(e);

            e.Effects = IsValidDropTarget(e) ? DragDropEffects.Move : DragDropEffects.None;

            e.Handled = true;
        }

        protected override void OnDrop(DragEventArgs e)
        {
            base.OnDrop(e);

            if (e.Data.GetDataPresent(DataObjectName))
            {
                ElementTreeItemViewModel dragged = (ElementTreeItemViewModel)e.Data.GetData(DataObjectName);
                ElementTreeItemViewModel dropTarget = _viewModel;

                if ((dragged != null) &&
                    (dropTarget != null) &&
                    (dragged != dropTarget))
                {
                    int index = GetDropAtIndex(e);
                    Tuple<IDsmElement, IDsmElement, int> moveParameter = new Tuple<IDsmElement, IDsmElement, int>(dragged.Element, dropTarget.Element, index);
                    _viewModel.MoveCommand.Execute(moveParameter);
                }

                e.Effects = DragDropEffects.Move;
            }
            _viewModel.IsDropTarget = false;
            e.Handled = true;
        }

        private bool IsValidDropTarget(DragEventArgs e)
        {
            bool isValidDropTarget = false;

            if (e.Data.GetDataPresent(DataObjectName))
            {
                ElementTreeItemViewModel dragged = (ElementTreeItemViewModel)e.Data.GetData(DataObjectName);
                ElementTreeItemViewModel dropTarget = _viewModel;

                if ((dragged != null) &&
                    (dropTarget != null) &&
                    (!dropTarget.Element.IsRecursiveChildOf(dragged.Element)))
                {
                    isValidDropTarget = true;
                }
            }

            return isValidDropTarget;
        }

        private int GetDropAtIndex(DragEventArgs e)
        {
            Point point = e.GetPosition(this);

            double pitch = _theme.MatrixCellSize + 2.0;

            int index = (int)(point.Y / pitch);
            return index;
        }

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

                    //---- Left hand indicator
                    indicatorRect = new Rect(_viewModel.IsExpandable ? 12.0 : 1.0,
                            1.0, _indicatorWidth, ActualHeight - _theme.SpacingWidth);
                    if (_viewModel.IsConsumerIn  &&  _viewModel.IsProviderIn)
                    {
                        Rect top = new Rect(indicatorRect.X, indicatorRect.Y,
                                indicatorRect.Width, indicatorRect.Height/2);
                        Rect bottom = new Rect(indicatorRect.X, top.Bottom,
                                indicatorRect.Width, indicatorRect.Height/2);
                        dc.DrawRectangle(_theme.MatrixColorProvider, null, top);
                        dc.DrawRectangle(_theme.MatrixColorConsumer, null, bottom);
                    }
                    else if (_viewModel.IsConsumerIn)
                        dc.DrawRectangle(_theme.MatrixColorConsumer, null, indicatorRect);
                    else if (_viewModel.IsProviderIn)
                        dc.DrawRectangle(_theme.MatrixColorProvider, null, indicatorRect);

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