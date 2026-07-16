// SPDX-License-Identifier: GPL-3.0-or-later
using DsmSuite.DsmViewer.Application.Interfaces;
using DsmSuite.DsmViewer.Model.Interfaces;
using DsmSuite.DsmViewer.ViewModel.Common;
using DsmSuite.DsmViewer.ViewModel.Lists;
using DsmSuite.DsmViewer.ViewModel.Main;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace DsmSuite.DsmViewer.ViewModel.Matrix
{
    public class MatrixViewModel : ViewModelBase, IMatrixViewModel
    {
        private double _zoomLevel;
        private readonly IMainViewModel _mainViewModel;
        private readonly IDsmApplication _application;
        private readonly IEnumerable<IDsmElement> _rootElements;

        /// <summary>
        /// The ViewModels that constitute the provider tree. Each leaf is a row in the matrix,
        /// internal nodes are drawn vertically and have no row with cells.
        /// </summary>
        private ObservableCollection<ElementTreeItemViewModel> _elementViewModelTree;
        /// <summary>
        /// The leaves in <see cref="_elementViewModelTree"/>, in the order they are visualized.
        /// These correspond to the rows/columns of the matrix. Note that a column/consumer
        /// does NOT have its own ElementTreeItemViewModel. Every leaf VM is either a leaf element,
        /// or a collapsed element.
        /// </summary>
        private List<ElementTreeItemViewModel> _elementViewModelLeafs;

        private MatrixViewModelCoordinate _selectedRow;
        private MatrixViewModelCoordinate _selectedColumn;

        private MatrixViewModelCoordinate _hoveredRow;
        private MatrixViewModelCoordinate _hoveredColumn;

        private int _matrixSize;
        private bool _isMetricsViewExpanded;

        private List<List<MatrixColor>> _cellColors;
        private List<List<int>> _cellWeights;
        private List<MatrixColor> _columnColors;
        private List<int> _columnElementIds;
        private List<string> _metrics;
        private const int _nrWeightBuckets = 10; // Number of buckets (quantiles) for grouping cell weights.
        private List<List<double>> _weightPercentiles;  // The weight bucket for every cell as a percentile

        private ElementToolTipViewModel _columnHeaderTooltipViewModel;
        private CellToolTipViewModel _cellTooltipViewModel;

        private readonly Dictionary<MetricType, string> _metricTypeNames;
        private string _selectedMetricTypeName;
        private MetricType _selectedMetricType;
        private string _searchText = "";

        public MatrixViewModel(IMainViewModel mainViewModel, IDsmApplication application, IEnumerable<IDsmElement> rootElements)
        {
            _mainViewModel = mainViewModel;
            _application = application;
            _rootElements = rootElements;

            ToggleElementExpandedCommand = mainViewModel.ToggleElementExpandedCommand;

            SortElementCommand = mainViewModel.SortElementCommand;
            MoveUpElementCommand = mainViewModel.MoveUpElementCommand;
            MoveDownElementCommand = mainViewModel.MoveDownElementCommand;

            ToggleElementBookmarkCommand = mainViewModel.ToggleElementBookmarkCommand;

            AddChildElementCommand = mainViewModel.AddChildElementCommand;
            AddSiblingElementAboveCommand = mainViewModel.AddSiblingElementAboveCommand;
            AddSiblingElementBelowCommand = mainViewModel.AddSiblingElementBelowCommand;
            ModifyElementCommand = mainViewModel.ModifyElementCommand;
            ChangeElementParentCommand = mainViewModel.ChangeElementParentCommand;
            DeleteElementCommand = mainViewModel.DeleteElementCommand;

            CopyElementCommand = mainViewModel.DeleteElementCommand;
            CutElementCommand = mainViewModel.DeleteElementCommand;
            PasteAsChildElementCommand = mainViewModel.DeleteElementCommand;
            PasteAsSiblingElementAboveCommand = mainViewModel.DeleteElementCommand;
            PasteAsSiblingElementBelowCommand = mainViewModel.DeleteElementCommand;

            ShowElementIngoingRelationsCommand = RegisterCommand(ShowElementIngoingRelationsExecute);
            ShowElementOutgoingRelationCommand = RegisterCommand(ShowElementOutgoingRelationExecute);
            ShowElementinternalRelationsCommand = RegisterCommand(ShowElementinternalRelationsExecute);

            ShowElementConsumersCommand = RegisterCommand(ShowElementConsumersExecute);
            ShowElementProvidedInterfacesCommand = RegisterCommand(ShowProvidedInterfacesExecute);
            ShowElementRequiredInterfacesCommand = RegisterCommand(ShowElementRequiredInterfacesExecute);
            ShowCellDetailMatrixCommand = mainViewModel.ShowCellDetailMatrixCommand;

            ShowCellRelationsCommand = RegisterCommand(ShowCellRelationsExecute);
            ShowCellConsumersCommand = RegisterCommand(ShowCellConsumersExecute);
            ShowCellProvidersCommand = RegisterCommand(ShowCellProvidersExecute);
            ShowElementDetailMatrixCommand = mainViewModel.ShowElementDetailMatrixCommand;
            ShowElementContextMatrixCommand = mainViewModel.ShowElementContextMatrixCommand;

            ToggleMetricsViewExpandedCommand = RegisterCommand(ToggleMetricsViewExpandedExecute);

            PreviousMetricCommand = RegisterCommand(PreviousMetricExecute, PreviousMetricCanExecute);
            NextMetricCommand = RegisterCommand(NextMetricExecute, NextMetricCanExecute);

            Reload();

            ZoomLevel = 1.0;

            _metricTypeNames = new Dictionary<MetricType, string>
            {
                [MetricType.NumberOfElements] = "Internal\nElements",
                [MetricType.RelativeSizePercentage] = "Relative\nSize",
                [MetricType.IngoingRelations] = "Consuming\nRelations",
                [MetricType.OutgoingRelations] = "Providing\nRelations",
                [MetricType.InternalRelations] = "Internal\nRelations",
                [MetricType.ExternalRelations] = "External\nRelations",
                [MetricType.HierarchicalCycles] = "Hierarchical\nCycles",
                [MetricType.SystemCycles] = "System\nCycles",
                [MetricType.Cycles] = "Total\nCycles",
                [MetricType.CyclicityPercentage] = "Total\nCyclicity"
            };

            _selectedMetricType = MetricType.NumberOfElements;
            SelectedMetricTypeName = _metricTypeNames[_selectedMetricType];
        }
        public IEnumerable<string> MetricTypes => _metricTypeNames.Values;
        public string SelectedMetricTypeName
        {
            get { return _selectedMetricTypeName; }
            set
            {
                _selectedMetricTypeName = value;
                _selectedMetricType = _metricTypeNames.FirstOrDefault(x => x.Value == _selectedMetricTypeName).Key;
                Reload();
                RaisePropertyChanged();
            }
        }
        public IReadOnlyList<string> Metrics => _metrics;
        public bool IsMetricsViewExpanded
        {
            get { return _isMetricsViewExpanded; }
            set { _isMetricsViewExpanded = value; RaisePropertyChanged(); }
        }

        public int MatrixSize
        {
            get { return _matrixSize; }
            set { _matrixSize = value; RaisePropertyChanged(); }
        }
        public ObservableCollection<ElementTreeItemViewModel> ElementViewModelTree
        {
            get { return _elementViewModelTree; }
            private set { _elementViewModelTree = value; RaisePropertyChanged(); }
        }

        public IReadOnlyList<MatrixColor> ColumnColors => _columnColors;
        public IReadOnlyList<int> ColumnElementIds => _columnElementIds;
        public IReadOnlyList<IList<MatrixColor>> CellColors => _cellColors;
        public IReadOnlyList<IReadOnlyList<int>> CellWeights => _cellWeights;
        /// <summary>
        /// The weight percentile for every cell as a number between 0 and 1.
        /// These are not actual percentiles, but quantiles with <c>_nrWeightBuckets"</c> buckets,
        /// where bucket 0 is reserved for cells with weight 0.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<double>> WeightPercentiles => _weightPercentiles;


        public double ZoomLevel
        {
            get { return _zoomLevel; }
            set { _zoomLevel = value; RaisePropertyChanged(); }
        }

        private void ExpandElement(IDsmElement element)
        {
            IDsmElement current = element.Parent;
            while (current != null)
            {
                current.IsExpanded = true;
                current = current.Parent;
            }
            Reload();
        }


        /// <summary>
        /// Return the ViewModel for the given element, or null if it doesn't exist.
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        public ElementTreeItemViewModel FindElementViewModel(IDsmElement element)
        {
            return element == null ? null : FindElementViewModel(element, ElementViewModelTree);
        }


        /// <summary>
        /// Find the ViewModel for the given element by searching the trees in list recursively.
        /// Return null if no corresponding ViewModel can be found.
        /// </summary>
        private ElementTreeItemViewModel FindElementViewModel(IDsmElement element, IEnumerable<ElementTreeItemViewModel> list)
        {
            ElementTreeItemViewModel res = null;

            foreach (ElementTreeItemViewModel vm in list)
            {
                if (vm.Element == element)
                    return vm;
                if ((res = FindElementViewModel(element, vm.Children)) != null)
                    return res;
            }

            return null;
        }

        /// <summary>
        /// Add the leaf viewmodels strictly under a given element to leaves.
        /// </summary>
        private void FindLeaves(ElementTreeItemViewModel element,  List<ElementTreeItemViewModel> leaves)
        {
            foreach (ElementTreeItemViewModel vm in element.Children)
            {
                if (vm.IsExpanded)
                    FindLeaves(vm, leaves);
                else
                    leaves.Add(vm);
            }
        }

        private MatrixViewModelCoordinate GetRowCoord(int? row)
        {
            ElementTreeItemViewModel vm = null;

            if (row == null)
                return null;

            if (row is int therow  &&  therow < _elementViewModelLeafs.Count)
            {
                vm = _elementViewModelLeafs[therow];
            }

            return new()
            {
                Axis = MatrixViewModelCoordinate.AxisType.Row,
                Index = row,
                Element = vm?.Element
            };
        }

        public MatrixViewModelCoordinate GetRowCoord(ElementTreeItemViewModel treeItem)
        {
            int? selrow = null;

            if (treeItem != null)
            {
                for (int row = 0; row < _elementViewModelLeafs.Count; row++)
                {
                    if (_elementViewModelLeafs[row] == treeItem)
                    {
                        selrow = row;
                        break;
                    }
                }
            }

            return new()
            {
                Axis = MatrixViewModelCoordinate.AxisType.Row,
                Index = selrow,
                Element = treeItem?.Element
            };
        }

        private MatrixViewModelCoordinate GetColumnCoord(int? column)
        {
            IDsmElement consumer = null;

            if (column == null)
                return null;

            if (column is int thecol  &&  thecol < _elementViewModelLeafs.Count)
            {
                consumer = _elementViewModelLeafs[thecol].Element;
            }

            return new()
            {
                Axis = MatrixViewModelCoordinate.AxisType.Column,
                Index = column,
                Element = consumer
            };
        }

        public MatrixViewModelCoordinate GetColumnCoord(ElementTreeItemViewModel treeItem)
        {
            int? selrow = null;

            if (treeItem != null)
            {
                for (int row = 0; row < _elementViewModelLeafs.Count; row++)
                {
                    if (_elementViewModelLeafs[row] == treeItem)
                    {
                        selrow = row;
                        break;
                    }
                }
            }

            return new()
            {
                Axis = MatrixViewModelCoordinate.AxisType.Column,
                Index = selrow,
                Element = treeItem?.Element
            };
        }


        //=========================== Selecting ==============================
        #region Selecting

        /// <summary>
        /// The currently selected row, or null if no row is selected.
        /// </summary>
        public MatrixViewModelCoordinate SelectedRow
        {
            get { return _selectedRow; }
            private set { _selectedRow = value; SelectionChanged(); RaisePropertyChanged(); }
        }


        /// <summary>
        /// The column of the currently selected consumer, or null if no consumer is selected.
        /// </summary>
        public MatrixViewModelCoordinate SelectedColumn
        {
            get { return _selectedColumn; }
            private set { _selectedColumn = value; SelectionChanged(); RaisePropertyChanged(); }
        }

        /// <summary>
        /// Auxiliary function to update various properties after the selection has changed.
        /// </summary>
        private void SelectionChanged()
        {
            UpdateRelationFlags();
        }


        /// <summary>
        /// Selected the given row and deselect all columns.
        /// </summary>
        public void SelectRow(int? row)
        {
            SelectedRow = GetRowCoord(row);
            SelectedColumn = null;
            _mainViewModel.UpdateCommandStates();
        }

        /// <summary>
        /// Select the given column and deselect all rows.
        /// </summary>
        public void SelectColumn(int? column)
        {
            SelectedColumn = GetColumnCoord(column);
            SelectedRow = null;
            _mainViewModel.UpdateCommandStates();
        }

        /// <summary>
        /// Select the given row and column.
        /// </summary>
        public void SelectCell(int? row, int? column)
        {
            SelectedRow = GetRowCoord(row);
            SelectedColumn = GetColumnCoord(column);
            _mainViewModel.UpdateCommandStates();
        }

        /// <summary>
        /// Select the given tree item (provider) and deselect all columns.
        /// </summary>
        public void SelectTreeItem(ElementTreeItemViewModel selectedTreeItem)
        {
            SelectedRow = GetRowCoord(selectedTreeItem);
            SelectedColumn = null;
            _mainViewModel.UpdateCommandStates();
        }

        #endregion

        //====================== Hovering =====================================
        #region Hovering

        public MatrixViewModelCoordinate HoveredRow
        {
            get { return _hoveredRow; }
            private set { _hoveredRow = value; RaisePropertyChanged(); }
        }

        public MatrixViewModelCoordinate HoveredColumn
        {
            get { return _hoveredColumn; }
            private set { _hoveredColumn = value; RaisePropertyChanged(); }
        }

 
        public void HoverRow(int? row)
        {
            HoveredRow = GetRowCoord(row);
            HoveredColumn = null;
        }

        public void HoverColumn(int? column)
        {
            HoveredRow = null;
            HoveredColumn = GetColumnCoord(column);
            UpdateColumnHeaderTooltip(column);
        }

        public void HoverCell(int? row, int? column)
        {
            HoveredRow = GetRowCoord(row);
            HoveredColumn = GetColumnCoord(column);
            UpdateCellTooltip(row, column);
        }

        public void HoverTreeItem(ElementTreeItemViewModel hoveredTreeItem)
        {
            HoveredRow = GetRowCoord(hoveredTreeItem);
            HoveredColumn = null;
        }

        #endregion

        //====================== Visualization ===============================
        #region Visualization

        public ElementToolTipViewModel ColumnHeaderToolTipViewModel
        {
            get { return _columnHeaderTooltipViewModel; }
            set { _columnHeaderTooltipViewModel = value; RaisePropertyChanged(); }
        }

        public CellToolTipViewModel CellToolTipViewModel
        {
            get { return _cellTooltipViewModel; }
            set { _cellTooltipViewModel = value; RaisePropertyChanged(); }
        }
 
        private void DefineCellColors()
        {
            int matrixSize = _elementViewModelLeafs.Count;

            _cellColors = new List<List<MatrixColor>>();

            // Define background color
            for (int row = 0; row < matrixSize; row++)
            {
                _cellColors.Add(new List<MatrixColor>());
                for (int column = 0; column < matrixSize; column++)
                {
                    _cellColors[row].Add(MatrixColor.Background);
                }
            }

            // Define expanded block color
            for (int row = 0; row < matrixSize; row++)
            {
                ElementTreeItemViewModel viewModel = _elementViewModelLeafs[row];

                Stack<ElementTreeItemViewModel> viewModelHierarchy = new Stack<ElementTreeItemViewModel>();
                ElementTreeItemViewModel child = viewModel;
                ElementTreeItemViewModel parent = viewModel.Parent;
                while ((parent != null) && (parent.Children[0] == child))
                {
                    viewModelHierarchy.Push(parent);
                    child = parent;
                    parent = parent.Parent;
                }

                foreach (ElementTreeItemViewModel currentViewModel in viewModelHierarchy)
                {
                    int leafElements = 0;
                    CountLeafElements(currentViewModel.Element, ref leafElements);

                    if (leafElements > 0 && currentViewModel.Depth > 0)
                    {
                        MatrixColor expandedColor = MatrixColorConverter.GetColor(currentViewModel.Depth);

                        int begin = row;
                        int end = row + leafElements;

                        for (int rowDelta = begin; rowDelta < end; rowDelta++)
                        {
                            for (int columnDelta = begin; columnDelta < end; columnDelta++)
                            {
                                _cellColors[rowDelta][columnDelta] = expandedColor;
                            }
                        }
                    }
                }
            }

            // Define diagonal color
            for (int row = 0; row < matrixSize; row++)
            {
                int depth = _elementViewModelLeafs[row].Depth;
                MatrixColor dialogColor = MatrixColorConverter.GetColor(depth);
                _cellColors[row][row] = dialogColor;
            }

            // Define cycle color
            for (int row = 0; row < matrixSize; row++)
            {
                for (int column = 0; column < matrixSize; column++)
                {
                    IDsmElement consumer = _elementViewModelLeafs[column].Element;
                    IDsmElement provider = _elementViewModelLeafs[row].Element;
                    CycleType cycleType = _application.IsCyclicDependency(consumer, provider);
                    if (cycleType != CycleType.None)
                    {
                        _cellColors[row][column] = MatrixColor.Cycle;
                    }
                }
            }
        }

        private void CountLeafElements(IDsmElement element, ref int count)
        {
            if (!element.IsExpanded)
            {
                count++;
            }
            else
            {
                foreach (IDsmElement child in element.Children)
                {
                    CountLeafElements(child, ref count);
                }
            }
        }

        private void DefineColumnColors()
        {
            _columnColors = new List<MatrixColor>();
            foreach (ElementTreeItemViewModel provider in _elementViewModelLeafs)
            {
                _columnColors.Add(provider.Color);
            }
        }

        private void DefineColumnContent()
        {
            _columnElementIds = new List<int>();
            foreach (ElementTreeItemViewModel provider in _elementViewModelLeafs)
            {
                _columnElementIds.Add(provider.Element.Order);
            }
        }

        /// <summary>
        /// For every cell, set its weight and its weight bucket.
        /// </summary>
        private void DefineCellContent()
        {
            List<int> sortedWeights = new List<int>();
            List<int> buckets = new List<int>(_nrWeightBuckets);

            int matrixSize = _elementViewModelLeafs.Count;

            //---- Set weight for every cell
            _cellWeights = new List<List<int>>();
            for (int row = 0; row < matrixSize; row++)
            {
                _cellWeights.Add(new List<int>());
                for (int column = 0; column < matrixSize; column++)
                {
                    IDsmElement consumer = _elementViewModelLeafs[column].Element;
                    IDsmElement provider = _elementViewModelLeafs[row].Element;
                    int weight = _application.GetDependencyWeight(consumer, provider);
                    _cellWeights[row].Add(weight);
                    if (weight > 0)
                        sortedWeights.Add(weight);
                }
            }

            //---- Set up weight buckets
            buckets.Add(0);
            if (sortedWeights.Count > 0)
            {
                sortedWeights.Sort();
                int stepSize = sortedWeights.Count / _nrWeightBuckets;
                for (int i = 1; i < _nrWeightBuckets; i++)
                {
                    buckets.Add(sortedWeights[i * stepSize]);
                }
            }

            //---- Assign every cell its weight percentile
            _weightPercentiles = new List<List<double>>();
            for (int row = 0; row < matrixSize; row++)
            {
                _weightPercentiles.Add(new List<double>());
                for (int column = 0; column < matrixSize; column++)
                {
                    int i = buckets.Count-1;
                    while (_cellWeights[row][column] < buckets[i])
                        i--;
                    if (i == 0)     // Bucket 0 is for weight 0 exclusively
                        i = 1;
                    _weightPercentiles[row].Add(i / (double) _nrWeightBuckets);
                }
            }
        }
 
        private void UpdateColumnHeaderTooltip(int? column)
        {
            if (column.HasValue)
            {
                IDsmElement element = _elementViewModelLeafs[column.Value].Element;
                if (element != null)
                {
                    ColumnHeaderToolTipViewModel = new ElementToolTipViewModel(element, _application);
                }
            }
        }

        private void UpdateCellTooltip(int? row, int? column)
        {
            if (row.HasValue && column.HasValue)
            {
                IDsmElement consumer = _elementViewModelLeafs[column.Value].Element;
                IDsmElement provider = _elementViewModelLeafs[row.Value].Element;

                if ((consumer != null) && (provider != null))
                {
                    int weight = _application.GetDependencyWeight(consumer, provider);
                    CycleType cycleType = _application.IsCyclicDependency(consumer, provider);
                    CellToolTipViewModel = new CellToolTipViewModel(consumer, provider, weight, cycleType);
                }
            }
        }


        /// <summary>
        /// Set the <c>IsConsumer, IsProvider</c> properties of the view model leaves with respect to
        /// the <c>SelectedRow</c>.
        /// </summary>
        private void UpdateRelationFlags()
        {
            if (SelectedRow?.Element == null)
            {
                foreach (ElementTreeItemViewModel row in _elementViewModelLeafs)
                {
                    row.IsConsumer = false;
                    row.IsProvider = false;
                    row.IsConsumerIn = false;
                    row.IsProviderIn = false;
                }
            }
            else
            {
                foreach (ElementTreeItemViewModel row in _elementViewModelLeafs)
                {
                    row.IsConsumer = _application.GetDependencyWeight(row.Element, SelectedRow.Element) > 0;
                    row.IsProvider = _application.GetDependencyWeight(SelectedRow.Element, row.Element) > 0;
                    row.IsConsumerIn = false;
                    row.IsProviderIn = false;
                }
            }

            if (SelectedRow?.Element?.IsExpanded == true)
            {
                List<ElementTreeItemViewModel> leaves = new();
                FindLeaves(FindElementViewModel(SelectedRow.Element), leaves);
                List<ElementTreeItemViewModel> others = new(_elementViewModelLeafs.Except(leaves));

                foreach (ElementTreeItemViewModel leaf in leaves)
                {
                    leaf.IsProviderIn = others
                            .Any(x => _application.GetDependencyWeight(x.Element, leaf.Element) > 0);
                    leaf.IsConsumerIn = others
                            .Any(x => _application.GetDependencyWeight(leaf.Element, x.Element) > 0);
                }
             }
        }

        #endregion

        //============================ Commands ==============================
        #region commands
        public ICommand ToggleElementExpandedCommand { get; }

        public ICommand SortElementCommand { get; }
        public ICommand MoveUpElementCommand { get; }
        public ICommand MoveDownElementCommand { get; }

        public ICommand ToggleElementBookmarkCommand { get; }

        public ICommand AddChildElementCommand { get; }
        public ICommand AddSiblingElementAboveCommand { get; }
        public ICommand AddSiblingElementBelowCommand { get; }
        public ICommand ModifyElementCommand { get; }
        public ICommand ChangeElementParentCommand { get; }
        public ICommand DeleteElementCommand { get; }

        public ICommand CopyElementCommand { get; }
        public ICommand CutElementCommand { get; }
        public ICommand PasteAsChildElementCommand { get; }
        public ICommand PasteAsSiblingElementAboveCommand { get; }
        public ICommand PasteAsSiblingElementBelowCommand { get; }

        public ICommand ShowElementIngoingRelationsCommand { get; }
        public ICommand ShowElementOutgoingRelationCommand { get; }
        public ICommand ShowElementinternalRelationsCommand { get; }

        public ICommand ShowElementConsumersCommand { get; }
        public ICommand ShowElementProvidedInterfacesCommand { get; }
        public ICommand ShowElementRequiredInterfacesCommand { get; }
        public ICommand ShowElementDetailMatrixCommand { get; }
        public ICommand ShowElementContextMatrixCommand { get; }

        public ICommand ShowCellRelationsCommand { get; }
        public ICommand ShowCellConsumersCommand { get; }
        public ICommand ShowCellProvidersCommand { get; }
        public ICommand ShowCellDetailMatrixCommand { get; }

        public ICommand PreviousMetricCommand { get; }
        public ICommand NextMetricCommand { get; }

        public ICommand ToggleMetricsViewExpandedCommand { get; }

        private void ShowCellConsumersExecute(object parameter)
        {
            _mainViewModel.NotifyElementsReportReady(ElementListViewModelType.RelationConsumers,
                    SelectedColumn?.Element, SelectedRow?.Element);
        }

        private void ShowCellProvidersExecute(object parameter)
        {
            _mainViewModel.NotifyElementsReportReady(ElementListViewModelType.RelationProviders,
                    SelectedColumn?.Element, SelectedRow?.Element);
        }

        private void ShowElementIngoingRelationsExecute(object parameter)
        {
            _mainViewModel.NotifyRelationsReportReady(RelationsListViewModelType.ElementIngoingRelations,
                    null, SelectedRow?.Element);
        }

        private void ShowElementOutgoingRelationExecute(object parameter)
        {
            var relations = _application.FindOutgoingRelations(SelectedRow?.Element);
            _mainViewModel.NotifyRelationsReportReady(RelationsListViewModelType.ElementOutgoingRelations,
                    null, SelectedRow?.Element);
        }

        private void ShowElementinternalRelationsExecute(object parameter)
        {
            _mainViewModel.NotifyRelationsReportReady(RelationsListViewModelType.ElementInternalRelations,
                    null, SelectedRow?.Element);
        }

        private void ShowElementConsumersExecute(object parameter)
        {
            _mainViewModel.NotifyElementsReportReady(ElementListViewModelType.ElementConsumers,
                    null, SelectedRow?.Element);
        }

        private void ShowProvidedInterfacesExecute(object parameter)
        {
            _mainViewModel.NotifyElementsReportReady(ElementListViewModelType.ElementProvidedInterface,
                    null, SelectedRow?.Element);
        }

        private void ShowElementRequiredInterfacesExecute(object parameter)
        {
            _mainViewModel.NotifyElementsReportReady(ElementListViewModelType.ElementRequiredInterface,
                    null, SelectedRow?.Element);
        }

        private void ShowCellRelationsExecute(object parameter)
        {
            _mainViewModel.NotifyRelationsReportReady(RelationsListViewModelType.ConsumerProviderRelations,
                    SelectedColumn?.Element, SelectedRow?.Element);
        }

        private void ToggleMetricsViewExpandedExecute(object parameter)
        {
            IsMetricsViewExpanded = !IsMetricsViewExpanded;
        }

        private void PreviousMetricExecute(object parameter)
        {
            _selectedMetricType--;
            SelectedMetricTypeName = _metricTypeNames[_selectedMetricType];
            NotifyCommandsCanExecuteChanged();
        }

        private bool PreviousMetricCanExecute(object parameter)
        {
            return _selectedMetricType != MetricType.NumberOfElements;
        }

        private void NextMetricExecute(object parameter)
        {
            _selectedMetricType++;
            SelectedMetricTypeName = _metricTypeNames[_selectedMetricType];
            NotifyCommandsCanExecuteChanged();
        }

        private bool NextMetricCanExecute(object parameter)
        {
            return _selectedMetricType != MetricType.CyclicityPercentage;
        }

        #endregion

         //======================= Load and reload ============================
        #region Reload

        public void Reload()
        {
            //---- Save selection
            IDsmElement selectedProvider = SelectedRow?.Element;
            IDsmElement selectedConsumer = SelectedColumn?.Element;

            //---- Reload
            ElementViewModelTree = CreateElementViewModelTree();
            _elementViewModelLeafs = FindLeafElementViewModels();
            DefineColumnColors();
            DefineColumnContent();
            DefineCellColors();
            DefineCellContent();
            DefineMetrics();
            MatrixSize = _elementViewModelLeafs.Count;

            //---- Restore selection (with new indices)
            SelectedRow = GetRowCoord(FindElementViewModel(selectedProvider));
            SelectedColumn = GetColumnCoord(FindElementViewModel(selectedConsumer));
         }


        private ObservableCollection<ElementTreeItemViewModel> CreateElementViewModelTree()
        {
            int depth = 0;
            ObservableCollection<ElementTreeItemViewModel> tree = new ObservableCollection<ElementTreeItemViewModel>();
            foreach (IDsmElement element in _rootElements)
            {
                ElementTreeItemViewModel viewModel = new ElementTreeItemViewModel(_mainViewModel, this, _application, element, depth);
                tree.Add(viewModel);
                AddElementViewModelChildren(viewModel);
            }
            return tree;
        }

        private void AddElementViewModelChildren(ElementTreeItemViewModel viewModel)
        {
            if (viewModel.Element.IsExpanded)
            {
                foreach (IDsmElement child in viewModel.Element.Children)
                {
                    ElementTreeItemViewModel childViewModel = new ElementTreeItemViewModel(_mainViewModel, this, _application, child, viewModel.Depth + 1);
                    viewModel.AddChild(childViewModel);
                    AddElementViewModelChildren(childViewModel);
                }
            }
            else
            {
                viewModel.ClearChildren();
            }
        }
        private List<ElementTreeItemViewModel> FindLeafElementViewModels()
        {
            List<ElementTreeItemViewModel> leafViewModels = new List<ElementTreeItemViewModel>();

            foreach (ElementTreeItemViewModel viewModel in ElementViewModelTree)
            {
                FindLeafElementViewModels(leafViewModels, viewModel);
            }

            return leafViewModels;
        }
        private void FindLeafElementViewModels(List<ElementTreeItemViewModel> leafViewModels, ElementTreeItemViewModel viewModel)
        {
            if (!viewModel.IsExpanded)
            {
                leafViewModels.Add(viewModel);
            }

            foreach (ElementTreeItemViewModel childViewModel in viewModel.Children)
            {
                FindLeafElementViewModels(leafViewModels, childViewModel);
            }
        }
        private void DefineMetrics()
        {
            _metrics = new List<string>();
            switch (_selectedMetricType)
            {
                case MetricType.NumberOfElements:
                    foreach (ElementTreeItemViewModel viewModel in _elementViewModelLeafs)
                    {
                        int childElementCount = _application.GetElementSize(viewModel.Element);
                        _metrics.Add($"{childElementCount}");
                    }
                    break;
                case MetricType.RelativeSizePercentage:
                    foreach (ElementTreeItemViewModel viewModel in _elementViewModelLeafs)
                    {
                        int childElementCount = _application.GetElementSize(viewModel.Element);
                        int totalElementCount = _application.GetElementCount();
                        double metricCount = (totalElementCount > 0) ? childElementCount * 100.0 / totalElementCount : 0;
                        _metrics.Add($"{metricCount:0.000} %");
                    }
                    break;
                case MetricType.IngoingRelations:
                    foreach (ElementTreeItemViewModel viewModel in _elementViewModelLeafs)
                    {
                        int metricCount = _application.FindIngoingRelations(viewModel.Element).Count();
                        _metrics.Add($"{metricCount}");
                    }
                    break;
                case MetricType.OutgoingRelations:
                    foreach (ElementTreeItemViewModel viewModel in _elementViewModelLeafs)
                    {
                        int metricCount = _application.FindOutgoingRelations(viewModel.Element).Count();
                        _metrics.Add($"{metricCount}");
                    }
                    break;
                case MetricType.InternalRelations:
                    foreach (ElementTreeItemViewModel viewModel in _elementViewModelLeafs)
                    {
                        int metricCount = _application.FindInternalRelations(viewModel.Element).Count();
                        _metrics.Add($"{metricCount}");
                    }
                    break;
                case MetricType.ExternalRelations:
                    foreach (ElementTreeItemViewModel viewModel in _elementViewModelLeafs)
                    {
                        int metricCount = _application.FindExternalRelations(viewModel.Element).Count();
                        _metrics.Add($"{metricCount}");
                    }
                    break;
                case MetricType.HierarchicalCycles:
                    foreach (ElementTreeItemViewModel viewModel in _elementViewModelLeafs)
                    {
                        int metricCount = _application.GetHierarchicalCycleCount(viewModel.Element);
                        _metrics.Add(metricCount > 0 ? $"{metricCount}" : "-");
                    }
                    break;
                case MetricType.SystemCycles:
                    foreach (ElementTreeItemViewModel viewModel in _elementViewModelLeafs)
                    {
                        int metricCount = _application.GetSystemCycleCount(viewModel.Element);
                        _metrics.Add(metricCount > 0 ? $"{metricCount}" : "-");
                    }
                    break;
                case MetricType.Cycles:
                    foreach (ElementTreeItemViewModel viewModel in _elementViewModelLeafs)
                    {
                        int metricCount = _application.GetHierarchicalCycleCount(viewModel.Element) +
                                          _application.GetSystemCycleCount(viewModel.Element);
                        _metrics.Add(metricCount > 0 ? $"{metricCount}" : "-");
                    }
                    break;
                case MetricType.CyclicityPercentage:
                    foreach (ElementTreeItemViewModel viewModel in _elementViewModelLeafs)
                    {
                        int cycleCount = _application.GetHierarchicalCycleCount(viewModel.Element) +
                                          _application.GetSystemCycleCount(viewModel.Element);
                        int relationCount = _application.FindInternalRelations(viewModel.Element).Count();
                        double metricCount = (relationCount > 0) ? (cycleCount * 100.0 / relationCount) : 0;
                        _metrics.Add(metricCount > 0 ? $"{metricCount:0.000} %" : "-");
                    }
                    break;
                default:
                    foreach (ElementTreeItemViewModel viewModel in _elementViewModelLeafs)
                    {
                        _metrics.Add("");
                    }
                    break;
            }
        }

        #endregion
    }
}
