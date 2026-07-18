// SPDX-License-Identifier: GPL-3.0-or-later
using DsmSuite.DsmViewer.Application.Interfaces;
using DsmSuite.DsmViewer.Model.Interfaces;
using DsmSuite.DsmViewer.ViewModel.Common;
using DsmSuite.DsmViewer.ViewModel.Main;
using System.Windows.Input;

namespace DsmSuite.DsmViewer.ViewModel.Matrix
{
    public class ElementTreeItemViewModel : ViewModelBase
    {
        private readonly List<ElementTreeItemViewModel> _children;
        private ElementTreeItemViewModel _parent;
        private MatrixColor _color;

        public ElementTreeItemViewModel(IMainViewModel mainViewModel, IMatrixViewModel matrixViewModel, IDsmApplication application, IDsmElement element, int depth)
        {
            _children = new List<ElementTreeItemViewModel>();
            _parent = null;
            Element = element;
            Depth = depth;
            UpdateColor();

            // Removed 2026-07 for CSharpCodeAnalyst: MoveCommand (= ChangeElementParentCommand), which only
            // the drag and drop drop handler executed. See MatrixRowHeaderItemView.
            MoveUpElementCommand = matrixViewModel.MoveUpElementCommand;
            MoveDownElementCommand = matrixViewModel.MoveDownElementCommand;
            SortElementCommand = matrixViewModel.SortElementCommand;
            ToggleElementExpandedCommand = matrixViewModel.ToggleElementExpandedCommand;
            BookmarkElementCommand = matrixViewModel.ToggleElementBookmarkCommand;

            SelectedIndicatorViewMode = mainViewModel.SelectedIndicatorViewMode;

            ToolTipViewModel = new ElementToolTipViewModel(Element, application);
        }

        public ElementToolTipViewModel ToolTipViewModel { get; }
        public IDsmElement Element { get; }

        // Removed 2026-07 for CSharpCodeAnalyst: IsDropTarget, which coloured a row while it was hovered
        // during a drag. Drag and drop is gone, see MatrixRowHeaderItemView.

        public MatrixColor Color
        {
            get { return _color; }
            set { _color = value; RaisePropertyChanged();  }
        }

        public int Depth { get; }

        public int Id => Element.Id;
        public int Order => Element.Order;
        /// <summary>True iff this is a consumer of the selected tree item.</summary>
        public bool IsConsumer { get; set; }
        /// <summary>True iff this is a provider for the selected tree item.</summary>
        public bool IsProvider { get; set; }
        // Removed 2026-07 for CSharpCodeAnalyst: IsConsumerIn / IsProviderIn, which only ever fed the left
        // hand indicator. See MatrixRowHeaderItemView and MatrixViewModel.UpdateRelationFlags.
        public bool IsMatch => Element.IsMatch;
        public bool IsBookmarked => Element.IsBookmarked;
        public string Name => Element.IsRoot ? "Root" : Element.Name;

        public string Fullname => Element.Fullname;

        public ICommand MoveUpElementCommand { get; }
        public ICommand MoveDownElementCommand { get; }
        public ICommand SortElementCommand { get; }
        public ICommand ToggleElementExpandedCommand { get; }
        public ICommand BookmarkElementCommand { get; }

        public IndicatorViewMode SelectedIndicatorViewMode { get; }

        public bool IsExpandable => Element.HasChildren;

        public bool IsExpanded => Element.IsExpanded;


        public IReadOnlyList<ElementTreeItemViewModel> Children => _children;

        public ElementTreeItemViewModel Parent => _parent;

        public void AddChild(ElementTreeItemViewModel viewModel)
        {
            _children.Add(viewModel);
            viewModel._parent = this;
        }

        public void ClearChildren()
        {
            foreach (ElementTreeItemViewModel viewModel in _children)
            {
                viewModel._parent = null;
            }
            _children.Clear();
        }

        public int LeafElementCount
        {
            get
            {
                int count = 0;
                CountLeafElements(this, ref count);
                return count;
            }
        }

        private void CountLeafElements(ElementTreeItemViewModel viewModel, ref int count)
        {
            if (viewModel.Children.Count == 0)
            {
                count++;
            }
            else
            {
                foreach (ElementTreeItemViewModel child in viewModel.Children)
                {
                    CountLeafElements(child, ref count);
                }
            }
        }

        // Changed 2026-07 for CSharpCodeAnalyst: the drop target case is gone with drag and drop, so the
        // colour is only ever the nesting depth now.
        private void UpdateColor()
        {
            Color = MatrixColorConverter.GetColor(Depth);
        }
    }
}
