// SPDX-License-Identifier: GPL-3.0-or-later
using DsmSuite.Common.Util;
using DsmSuite.DsmViewer.Application.Interfaces;
using DsmSuite.DsmViewer.Application.Actions.Snapshot;
using DsmSuite.DsmViewer.Model.Interfaces;
using DsmSuite.DsmViewer.ViewModel.Common;
using DsmSuite.DsmViewer.ViewModel.Editing.Element;
using DsmSuite.DsmViewer.ViewModel.Editing.Snapshot;
using DsmSuite.DsmViewer.ViewModel.Lists;
using DsmSuite.DsmViewer.ViewModel.Matrix;
using DsmSuite.DsmViewer.ViewModel.Settings;
using System.Reflection;
using System.Diagnostics;
using System.Windows.Input;
using System.Collections.ObjectModel;
using System.IO;

namespace DsmSuite.DsmViewer.ViewModel.Main
{
    public class MainViewModel : ViewModelBase, IMainViewModel
    {
        public void NotifyElementsReportReady(ElementListViewModelType viewModelType, IDsmElement selectedConsumer, IDsmElement selectedProvider)
        {
            ElementListViewModel elementListViewModel = new ElementListViewModel(viewModelType, _application, selectedConsumer, selectedProvider);
            ElementsReportReady?.Invoke(this, elementListViewModel);
        }

        public void NotifyRelationsReportReady(RelationsListViewModelType viewModelType, IDsmElement selectedConsumer, IDsmElement selectedProvider)
        {
            RelationListViewModel viewModel = new RelationListViewModel(viewModelType, _application, selectedConsumer, selectedProvider);
            RelationsReportReady?.Invoke(this, viewModel);
        }

        public event EventHandler<ElementEditViewModel> ElementEditStarted;

        public event EventHandler<SnapshotMakeViewModel> SnapshotMakeStarted;

        public event EventHandler<ElementListViewModel> ElementsReportReady;
        public event EventHandler<RelationListViewModel> RelationsReportReady;

        public event EventHandler<ActionListViewModel> ActionsVisible;

        public event EventHandler<SettingsViewModel> SettingsVisible;

        public event EventHandler ScreenshotRequested;
        public event EventHandler GotoSnapshotExecuted;

        private readonly IDsmApplication _application;
        private string _modelFilename;
        private string _title;
        private string _version;

        private bool _isModified;
        private bool _isLoaded;
        private readonly double _minZoom = 0.50;
        private readonly double _maxZoom = 2.00;
        private readonly double _zoomFactor = 1.25;

        private MatrixViewModel _activeMatrix;

        private readonly ProgressViewModel _progressViewModel;
        private string _redoText;
        private string _undoText;
        private string _selectedSortAlgorithm;
        private IndicatorViewMode _selectedIndicatorViewMode;

        public MainViewModel(IDsmApplication application)
        {
            _application = application;
            _application.Modified += OnModelModified;
            _application.ActionPerformed += OnActionPerformed;

            OpenFileCommand = RegisterCommand(OpenFileExecute, OpenFileCanExecute);
            SaveFileCommand = RegisterCommand(SaveFileExecute, SaveFileCanExecute);

            HomeCommand = RegisterCommand(HomeExecute, HomeCanExecute);

            MoveUpElementCommand = RegisterCommand(MoveUpElementExecute, MoveUpElementCanExecute);
            MoveDownElementCommand = RegisterCommand(MoveDownElementExecute, MoveDownElementCanExecute);
            SortElementCommand = RegisterCommand(SortElementExecute, SortElementCanExecute);

            ToggleElementBookmarkCommand = RegisterCommand(ToggleElementBookmarkExecute, ToggleElementBookmarkCanExecute);

            ShowElementDetailMatrixCommand = RegisterCommand(ShowElementDetailMatrixExecute);
            ShowElementContextMatrixCommand = RegisterCommand(ShowElementContextMatrixExecute);
            ShowCellDetailMatrixCommand = RegisterCommand(ShowCellDetailMatrixExecute);

            ZoomInCommand = RegisterCommand(ZoomInExecute, ZoomInCanExecute);
            ZoomOutCommand = RegisterCommand(ZoomOutExecute, ZoomOutCanExecute);
            ToggleElementExpandedCommand = RegisterCommand(ToggleElementExpandedExecute);

            UndoCommand = RegisterCommand(UndoExecute, UndoCanExecute);
            RedoCommand = RegisterCommand(RedoExecute, RedoCanExecute);

            AddChildElementCommand = RegisterCommand(AddChildElementExecute);
            AddSiblingElementAboveCommand = RegisterCommand(AddSiblingElementAboveExecute);
            AddSiblingElementBelowCommand = RegisterCommand(AddSiblingElementBelowExecute);
            ModifyElementCommand = RegisterCommand(ModifyElementExecute, SelectedProviderIsNotRoot);
            DeleteElementCommand = RegisterCommand(DeleteElementExecute, SelectedProviderIsNotRoot);
            ChangeElementParentCommand = RegisterCommand(MoveElementExecute, SelectedProviderIsNotRoot);

            CopyElementCommand = RegisterCommand(CopyElementExecute, SelectedProviderIsNotRoot);
            CutElementCommand = RegisterCommand(CutElementExecute, SelectedProviderIsNotRoot);
            PasteAsChildElementCommand = RegisterCommand(PasteAsChildElementExecute, SelectedProviderIsNotRoot);
            PasteAsSiblingElementAboveCommand = RegisterCommand(PasteAsSiblingElementAboveExecute, SelectedProviderIsNotRoot);
            PasteAsSiblingElementBelowCommand = RegisterCommand(PasteAsSiblingElementBelowExecute, SelectedProviderIsNotRoot);

            MakeSnapshotCommand = RegisterCommand(MakeSnapshotExecute);
            GotoSnapshotCommand = RegisterCommand(GotoSnapshotExecute);
            ShowHistoryCommand = RegisterCommand(ShowHistoryExecute);
            ShowSettingsCommand = RegisterCommand(ShowSettingsExecute);

            TakeScreenshotCommand = RegisterCommand(TakeScreenshotExecute);

            _modelFilename = "";
            _version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            _title = $"DSM Viewer";
            IsModified = false;
            _isLoaded = false;
            _selectedSortAlgorithm = SupportedSortAlgorithms[0];
            _selectedIndicatorViewMode = IndicatorViewMode.Default;
            _progressViewModel = new ProgressViewModel();
            Snapshots = new();

            ActiveMatrix = new MatrixViewModel(this, _application, new List<IDsmElement>());
            ElementSearchViewModel = new ElementSearchViewModel(_application, null, null, null, true);
            ElementSearchViewModel.SearchUpdated += OnSearchUpdated;
        }

        private void OnSearchUpdated(object sender, EventArgs e)
        {
            SelectDefaultIndicatorMode();
            ActiveMatrix.Reload();
        }

        private void OnModelModified(object sender, bool e)
        {
            IsModified = e;
        }


        /// <summary>
        /// Convenience method.
        /// </summary>
        private IDsmElement SelectedProvider => ActiveMatrix?.SelectedRow?.Element;


        public MatrixViewModel ActiveMatrix
        {
            get { return _activeMatrix; }
            set { _activeMatrix = value; RaisePropertyChanged(); }
        }

        public ElementSearchViewModel ElementSearchViewModel { get; }

        private bool _isMetricsViewExpanded;

        public bool IsMetricsViewExpanded
        {
            get { return _isMetricsViewExpanded; }
            set { _isMetricsViewExpanded = value; RaisePropertyChanged(); }
        }

        public List<string> SupportedSortAlgorithms => _application.GetSupportedSortAlgorithms().ToList();

        public string SelectedSortAlgorithm
        {
            get { return _selectedSortAlgorithm; }
            set { _selectedSortAlgorithm = value; RaisePropertyChanged(); }
        }

        public List<IndicatorViewMode> SupportedIndicatorViewModes => Enum.GetValues(typeof(IndicatorViewMode)).Cast<IndicatorViewMode>().ToList();

        public IndicatorViewMode SelectedIndicatorViewMode
        {
            get { return _selectedIndicatorViewMode; }
            set { _selectedIndicatorViewMode = value; RaisePropertyChanged(); ActiveMatrix?.Reload(); }
        }

        /// <summary>
        /// The snapshots that can be used in a GotoSnapShot command.
        /// </summary>
        public ObservableCollection<MakeSnapshotAction> Snapshots { get; }

        public ICommand OpenFileCommand { get; }
        public ICommand SaveFileCommand { get; }
        public ICommand HomeCommand { get; }

        public ICommand MoveUpElementCommand { get; }
        public ICommand MoveDownElementCommand { get; }

        public ICommand ToggleElementBookmarkCommand { get; }

        public ICommand SortElementCommand { get; }
        public ICommand ShowElementDetailMatrixCommand { get; }
        public ICommand ShowElementContextMatrixCommand { get; }
        public ICommand ShowCellDetailMatrixCommand { get; }
        public ICommand ZoomInCommand { get; }
        public ICommand ZoomOutCommand { get; }
        public ICommand ToggleElementExpandedCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }

        public ICommand AddChildElementCommand { get; }
        public ICommand AddSiblingElementAboveCommand { get; }
        public ICommand AddSiblingElementBelowCommand { get; }
        public ICommand ModifyElementCommand { get; }
        public ICommand DeleteElementCommand { get; }
        public ICommand ChangeElementParentCommand { get; }

        public ICommand CopyElementCommand { get; }
        public ICommand CutElementCommand { get; }
        public ICommand PasteAsChildElementCommand { get; }
        public ICommand PasteAsSiblingElementAboveCommand { get; }
        public ICommand PasteAsSiblingElementBelowCommand { get; }

        public ICommand GotoSnapshotCommand { get; }
        public ICommand MakeSnapshotCommand { get; }
        public ICommand ShowHistoryCommand { get; }
        public ICommand ShowSettingsCommand { get; }
        public ICommand TakeScreenshotCommand { get; }

        public string ModelFilename
        {
            get { return _modelFilename; }
            set { _modelFilename = value; RaisePropertyChanged(); }
        }

        public bool IsModified
        {
            get { return _isModified; }
            set { _isModified = value; RaisePropertyChanged(); NotifyCommandsCanExecuteChanged(); }
        }

        public bool IsLoaded
        {
            get { return _isLoaded; }
            set { _isLoaded = value; RaisePropertyChanged(); NotifyCommandsCanExecuteChanged(); }
        }

        public string Title
        {
            get { return _title; }
            set { _title = value; RaisePropertyChanged(); }
        }

        public string Version
        {
            get { return _version; }
            set { _version = value; RaisePropertyChanged(); }
        }

        public ProgressViewModel ProgressViewModel => _progressViewModel;

        private async void OpenFileExecute(object parameter)
        {
            var progress = new Progress<ProgressInfo>(p =>
            {
                _progressViewModel.Update(p);
            });

            _progressViewModel.Action = "Reading";
            string filename = parameter as string;
            if (filename != null)
            {
                FileInfo fileInfo = new FileInfo(filename);

                switch (fileInfo.Extension)
                {
                    case ".dsm":
                        {
                            FileInfo dsmFileInfo = fileInfo;
                            await _application.AsyncOpenModel(dsmFileInfo.FullName, progress);
                            ModelFilename = dsmFileInfo.FullName;
                            Title = $"DSM Viewer - {dsmFileInfo.Name}";
                            IsLoaded = true;
                        }
                        break;
                    case ".dsi":
                        {
                            FileInfo dsiFileInfo = fileInfo;
                            FileInfo dsmFileInfo = new FileInfo(fileInfo.FullName.Replace(".dsi", ".dsm"));
                            await _application.AsyncImportDsiModel(dsiFileInfo.FullName, dsmFileInfo.FullName, false, true, progress);
                            ModelFilename = dsmFileInfo.FullName;
                            Title = $"DSM Viewer - {dsmFileInfo.Name}";
                            IsLoaded = true;
                        }
                        break;
                    // Changed 2026-07 for CSharpCodeAnalyst: the ".sql" case was removed together
                    // with SqlImporter.
                }
                ActiveMatrix = new MatrixViewModel(this, _application, GetRootElements());
                UpdateSnapshots();
            }
        }

        /// <summary>
        /// Added 2026-07 for CSharpCodeAnalyst: show a model that was already populated in memory,
        /// without any file IO.
        /// </summary>
        /// <remarks>
        /// The caller populates an <see cref="IDsmModel"/> and passes it to the
        /// <see cref="IDsmApplication"/> constructor, so there is no model swap and no need to go
        /// through OpenFileExecute. Note that swapping the model afterwards (as the file based path
        /// does via LoadModel) leaves DsmApplication's DsmQueries bound to the previous model, so
        /// building the model up front is the only correct order here.
        /// </remarks>
        public void ShowInMemoryModel(string title)
        {
            Title = title;
            ModelFilename = null;
            IsLoaded = true;
            ActiveMatrix = new MatrixViewModel(this, _application, GetRootElements());
            UpdateSnapshots();
        }

        private bool OpenFileCanExecute(object parameter)
        {
            string fileToOpen = parameter as string;
            return fileToOpen != null  && File.Exists(fileToOpen);
        }

        private async void SaveFileExecute(object parameter)
        {
            var progress = new Progress<ProgressInfo>(p =>
            {
                _progressViewModel.Update(p);
            });

            _progressViewModel.Action = "Writing";
            await _application.AsyncSaveModel(ModelFilename, progress);
        }

        private bool SaveFileCanExecute(object parameter)
        {
            return IsModified;
        }

        private async void SaveAsExecute(object parameter)
        {
            var progress = new Progress<ProgressInfo>(p =>
            {
                _progressViewModel.Update(p);
            });

            _progressViewModel.Action = "Writing";

            string filename = parameter as string;
            if (filename != null)
                await _application.AsyncSaveModel(filename, progress);
        }

        private bool SaveAsCanExecute(object parameter)
        {
            return _application?.RootElement != null;
        }

        private void HomeExecute(object parameter)
        {
            _application.ShowElementDetail(_application.RootElement, null);

            // Added 2026-07 for CSharpCodeAnalyst: reset the zoom, like ShowCellDetailMatrixExecute. Reload
            // keeps the zoom, so returning to the full matrix would otherwise stay at whatever a preceding
            // drill left it at. 1.0 is the constructor default.
            if (ActiveMatrix != null)
            {
                ActiveMatrix.ZoomLevel = 1.0;
            }
        }

        private IEnumerable<IDsmElement> GetRootElements()
        {
            return new List<IDsmElement> { _application.RootElement };
        }

        private bool HomeCanExecute(object parameter)
        {
            return IsLoaded;
        }

        /// <summary>
        /// Sort the child elements of the selected provider. If shift is down, this is done
        /// recursively for all elements under the selected provider.
        /// </summary>
        private void SortElementExecute(object recursive)
        {
            //TODO doesn't belong in the viewmodel, recursive should be an argument
            bool rec = (Keyboard.Modifiers & ModifierKeys.Shift) > 0;
            if (rec)
                _application.SortRecursively(SelectedProvider, SelectedSortAlgorithm);
            else
                _application.Sort(SelectedProvider, SelectedSortAlgorithm);
        }

        private bool SortElementCanExecute(object parameter)
        {
            return _application.HasChildren(SelectedProvider);
        }

        private void ShowElementDetailMatrixExecute(object parameter)
        {
            _application.ShowElementDetail(SelectedProvider, null);
        }

        private void ShowElementContextMatrixExecute(object parameter)
        {
            _application.ShowElementContext(SelectedProvider);
        }

        private void ShowCellDetailMatrixExecute(object parameter)
        {
            _application.ShowElementDetail(SelectedProvider, ActiveMatrix?.SelectedColumn?.Element);

            // Added 2026-07 for CSharpCodeAnalyst: reset the zoom when drilling into a cell's relation
            // matrix. Reload keeps the zoom (it only rebuilds the tree), so drilling from a zoomed-out
            // overview would show the small detail matrix at that same tiny scale. 1.0 is the constructor
            // default.
            if (ActiveMatrix != null)
            {
                ActiveMatrix.ZoomLevel = 1.0;
            }
        }

        private void MoveUpElementExecute(object parameter)
        {
            _application.MoveUp(SelectedProvider);
        }

        private bool MoveUpElementCanExecute(object parameter)
        {
            IDsmElement current = SelectedProvider;
            IDsmElement previous = _application.PreviousSibling(current);
            return (current != null) && (previous != null);
        }

        private void MoveDownElementExecute(object parameter)
        {
            _application.MoveDown(SelectedProvider);
        }

        private bool MoveDownElementCanExecute(object parameter)
        {
            IDsmElement current = SelectedProvider;
            IDsmElement next = _application.NextSibling(current);
            return (current != null) && (next != null);
        }

        private void ZoomInExecute(object parameter)
        {
            if (ActiveMatrix != null)
            {
                ActiveMatrix.ZoomLevel *= _zoomFactor;
            }
        }

        private bool ZoomInCanExecute(object parameter)
        {
            return ActiveMatrix?.ZoomLevel < _maxZoom;
        }

        private void ZoomOutExecute(object parameter)
        {
            if (ActiveMatrix != null)
            {
                ActiveMatrix.ZoomLevel /= _zoomFactor;
            }
        }

        private bool ZoomOutCanExecute(object parameter)
        {
            return ActiveMatrix?.ZoomLevel > _minZoom;
        }

        /// <summary>
        /// Toggle the IsExpanded state of the current row. If <paramref name="recursive"/> is
        /// <c>bool true</c>, the IsExpanded state of the entire subtree under the current element
        /// is set to the same new value as element.
        /// If <paramref name="recursive"/> is false, only the current element is toggled.
        /// </summary>
        private void ToggleElementExpandedExecute(object recursive)
        {
            ElementTreeItemViewModel vm = ActiveMatrix.FindElementViewModel(ActiveMatrix.HoveredRow?.Element);
            if (vm == null  ||  !vm.IsExpandable)
                return;

            if ((bool)recursive)
                vm.Element.ExpandRecursively(!vm.IsExpanded);
            else
                vm.Element.IsExpanded = !vm.IsExpanded;

            ActiveMatrix.Reload();
        }

        public string UndoText
        {
            get { return _undoText; }
            set { _undoText = value; RaisePropertyChanged(); }
        }

        private void UndoExecute(object parameter)
        {
            _application.Undo();
        }

        private bool UndoCanExecute(object parameter)
        {
            return _application.CanUndo();
        }

        private void RedoExecute(object parameter)
        {
            _application.Redo();
        }

        public string RedoText
        {
            get { return _redoText; }
            set { _redoText = value; RaisePropertyChanged(); }
        }

        private bool RedoCanExecute(object parameter)
        {
            return _application.CanRedo();
        }

        private void SelectDefaultIndicatorMode()
        {
            SelectedIndicatorViewMode = string.IsNullOrEmpty(ElementSearchViewModel.SearchText) ? IndicatorViewMode.Default : IndicatorViewMode.Search;
        }

        private void OnActionPerformed(object sender, EventArgs e)
        {
            UndoText = $"Undo {_application.GetUndoActionDescription()}";
            RedoText = $"Redo {_application.GetRedoActionDescription()}";
            ActiveMatrix?.Reload();
            UpdateSnapshots();
            NotifyCommandsCanExecuteChanged();
        }

        public void UpdateCommandStates()
        {
            NotifyCommandsCanExecuteChanged();
        }

        private void UpdateSnapshots()
        {
            Snapshots.Clear();
            foreach (MakeSnapshotAction a in _application.GetAllActions().OfType<MakeSnapshotAction>())
                Snapshots.Add(a);
            RaisePropertyChanged(nameof(Snapshots));
        }

        private void AddChildElementExecute(object parameter)
        {
            ElementEditViewModel elementEditViewModel = new ElementEditViewModel(ElementEditViewModelType.AddChild, _application, SelectedProvider);
            ElementEditStarted?.Invoke(this, elementEditViewModel);
        }

        private void AddSiblingElementAboveExecute(object parameter)
        {
            ElementEditViewModel elementEditViewModel = new ElementEditViewModel(ElementEditViewModelType.AddSiblingAbove, _application, SelectedProvider);
            ElementEditStarted?.Invoke(this, elementEditViewModel);
        }

        private void AddSiblingElementBelowExecute(object parameter)
        {
            ElementEditViewModel elementEditViewModel = new ElementEditViewModel(ElementEditViewModelType.AddSiblingBelow, _application, SelectedProvider);
            ElementEditStarted?.Invoke(this, elementEditViewModel);
        }

        private void ModifyElementExecute(object parameter)
        {
            ElementEditViewModel elementEditViewModel = new ElementEditViewModel(ElementEditViewModelType.Modify, _application, SelectedProvider);
            ElementEditStarted?.Invoke(this, elementEditViewModel);
        }

        private bool SelectedProviderIsNotRoot(object parameter)
        {
            return SelectedProvider != null  &&  !SelectedProvider.IsRoot;
        }

        private void DeleteElementExecute(object parameter)
        {
            _application.DeleteElement(SelectedProvider);
        }

        private void MoveElementExecute(object parameter)
        {
            Tuple<IDsmElement, IDsmElement, int> moveParameter = parameter as Tuple<IDsmElement, IDsmElement, int>;
            if (moveParameter != null)
            {
                _application.ChangeElementParent(moveParameter.Item1, moveParameter.Item2, moveParameter.Item3);
                // TODO Fix CommandManager.InvalidateRequerySuggested();
            }
        }

        private void CopyElementExecute(object parameter)
        {
            _application.CopyElement(SelectedProvider);
        }

        private void CutElementExecute(object parameter)
        {
            _application.CutElement(SelectedProvider);
        }

        private void PasteAsChildElementExecute(object parameter)
        {
            _application.PasteElement(SelectedProvider.Parent, 0);
        }

        private void PasteAsSiblingElementAboveExecute(object parameter)
        {
        }

        private void PasteAsSiblingElementBelowExecute(object parameter)
        {
        }

        private void ToggleElementBookmarkExecute(object parameter)
        {
            if (SelectedProvider != null)
            {
                SelectedProvider.IsBookmarked = !SelectedProvider.IsBookmarked;
                ActiveMatrix?.Reload();
            }
        }

        private bool ToggleElementBookmarkCanExecute(object parameter)
        {
            return _selectedIndicatorViewMode == IndicatorViewMode.Bookmarks;
        }

        private void MakeSnapshotExecute(object parameter)
        {
            SnapshotMakeViewModel viewModel = new SnapshotMakeViewModel(_application);
            SnapshotMakeStarted?.Invoke(this, viewModel);
        }

        private void GotoSnapshotExecute(object parameter)
        {
            MakeSnapshotAction action = parameter as MakeSnapshotAction;
            _application.GotoAction(action);
            GotoSnapshotExecuted?.Invoke(this, EventArgs.Empty);
            ActiveMatrix?.Reload();
        }


        private void ShowHistoryExecute(object parameter)
        {
            ActionListViewModel viewModel = new ActionListViewModel(_application);
            ActionsVisible?.Invoke(this, viewModel);
        }

        private void ShowSettingsExecute(object parameter)
        {
            SettingsViewModel viewModel = new SettingsViewModel(_application);
            SettingsVisible?.Invoke(this, viewModel);
            ActiveMatrix?.Reload();
        }

        private void TakeScreenshotExecute(object parameter)
        {
            ScreenshotRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
