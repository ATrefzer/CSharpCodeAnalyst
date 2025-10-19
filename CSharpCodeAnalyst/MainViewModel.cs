using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using CodeParser.Analysis.Cycles;
using CodeParser.Analysis.Shared;
using CodeParser.Extensions;
using CodeParser.Parser.Config;
using Contracts.Graph;
using CSharpCodeAnalyst.Analyzers;
using CSharpCodeAnalyst.Areas.AdvancedSearchArea;
using CSharpCodeAnalyst.Areas.CycleGroupsArea;
using CSharpCodeAnalyst.Areas.GraphArea;
using CSharpCodeAnalyst.Areas.InfoArea;
using CSharpCodeAnalyst.Areas.MetricArea;
using CSharpCodeAnalyst.Areas.PartitionsArea;
using CSharpCodeAnalyst.Areas.Shared;
using CSharpCodeAnalyst.Areas.TreeArea;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.Configuration;
using CSharpCodeAnalyst.Exploration;
using CSharpCodeAnalyst.Export;
using CSharpCodeAnalyst.Filter;
using CSharpCodeAnalyst.Gallery;
using CSharpCodeAnalyst.Help;
using CSharpCodeAnalyst.Import;
using CSharpCodeAnalyst.Messages;
using CSharpCodeAnalyst.Project;
using CSharpCodeAnalyst.Refactoring;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.Contracts;
using CSharpCodeAnalyst.Shared.DynamicDataGrid.Contracts.TabularData;
using CSharpCodeAnalyst.Shared.Messages;
using CSharpCodeAnalyst.Wpf;

namespace CSharpCodeAnalyst;

internal enum DirtyState
{
    Saved,
    Dirty,
    DirtyForceNewFile
}

internal sealed class MainViewModel : INotifyPropertyChanged
{
    private const int InfoPanelTabIndex = 2;
    private readonly AnalyzerManager _analyzerManager;
    private readonly Exporter _exporter;
    private readonly Importer _importer;

    private readonly MessageBus _messaging;
    private readonly Project.Project _project;

    private readonly ProjectExclusionRegExCollection _projectExclusionFilters;
    private readonly RefactoringService _refactoringService;
    private readonly IUserNotification _ui;
    private readonly UserSettings _userSettings;
    private Table? _analyzerResult;
    private ApplicationSettings _applicationSettings;
    private CodeGraph? _codeGraph;

    private Table? _cycles;

    private DirtyState _dirtyState = DirtyState.Saved;
    private Gallery.Gallery? _gallery;

    private GraphViewModel? _graphViewModel;
    private InfoPanelViewModel? _infoPanelViewModel;

    private bool _isCanvasHintsVisible = true;

    private bool _isGraphToolPanelVisible = true;
    private bool _isLeftPanelExpanded = true;
    private bool _isLoading;

    private string _loadMessage;
    private ObservableCollection<IMetric> _metrics = [];
    private LegendDialog? _openedLegendDialog;
    private string _openProjectFilePath = string.Empty;
    private AdvancedSearchViewModel? _searchViewModel;

    private int _selectedLeftTabIndex;
    private int _selectedRightTabIndex;

    private TreeViewModel? _treeViewModel;


    internal MainViewModel(MessageBus messaging, ApplicationSettings settings, UserSettings userSettings, AnalyzerManager analyzerManager, RefactoringService refactoringService)
    {
        // Initialize settings
        _applicationSettings = settings;
        _userSettings = userSettings;
        _analyzerManager = analyzerManager;
        _refactoringService = refactoringService;

        analyzerManager.AnalyzerDataChanged += OnAnalyzerDataChanged;

        _ui = new WindowsUserNotification();
        _importer = new Importer(_ui);
        _exporter = new Exporter(_ui);
        _importer.ImportStateChanged += OnUpdateProgress;
        _project = new Project.Project(_ui);
        _project.LoadingStateChanged += OnUpdateProgress;


        // Table data
        _cycles = null;
        _analyzerResult = null;


        // Apply settings
        _projectExclusionFilters = new ProjectExclusionRegExCollection();

        try
        {
            _projectExclusionFilters.Initialize(_applicationSettings.DefaultProjectExcludeFilter);
        }
        catch
        {
            _projectExclusionFilters.Initialize("");
        }

        _messaging = messaging;
        _gallery = new Gallery.Gallery();
        SearchCommand = new WpfCommand(Search);
        LoadSolutionCommand = new WpfCommand(OnImportSolution);
        ImportJdepsCommand = new WpfCommand(OnImportJdeps);
        ImportPlainTextCommand = new WpfCommand(OnImportPlainText);

        LoadProjectCommand = new WpfCommand(OnLoadProject);
        SaveProjectCommand = new WpfCommand(OnSaveProject);
        GraphClearCommand = new WpfCommand(OnGraphClear);
        GraphLayoutCommand = new WpfCommand(OnGraphLayout);
        FindCyclesCommand = new WpfCommand(OnFindCycles);
        ExecuteAnalyzerCommand = new WpfCommand<string>(OnExecuteAnalyzer);

        ShowGalleryCommand = new WpfCommand(OnShowGallery);
        ShowLegendCommand = new WpfCommand(OnShowLegend);
        OpenFilterDialogCommand = new WpfCommand(OnOpenFilterDialog);
        OpenSettingsDialogCommand = new WpfCommand(OnOpenSettingsDialog);
        ExportToDgmlCommand = new WpfCommand(OnExportToDgml);
        ExportToPlantUmlCommand = new WpfCommand(OnExportToPlantUml);
        ExportToSvgCommand = new WpfCommand(OnExportToSvg);
        ExportToPngCommand = new WpfCommand<FrameworkElement>(OnExportToPng);
        ExportToDsiCommand = new WpfCommand(OnExportToDsi);
        ExportPlainTextCommand = new WpfCommand(OnExportPlainText);
        CopyBitmapToClipboardCommand = new WpfCommand<FrameworkElement>(OnCopyCanvasToClipboard);
        OpenRecentFileCommand = new WpfCommand<string>(OnOpenRecentFile);
        SnapshotCommand = new WpfCommand(OnSnapshot);
        RestoreCommand = new WpfCommand(OnRestore);

        _loadMessage = string.Empty;

        RefreshMru();
    }



    public WpfCommand ExportPlainTextCommand { get; set; }

    public ObservableCollection<Mru> RecentFiles { get; } = [];

    private string OpenProjectFilePath
    {
        get => _openProjectFilePath;
        set
        {
            if (value == _openProjectFilePath) return;
            _openProjectFilePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Title));
        }
    }

    private ICommand OpenRecentFileCommand { get; }

    public Table? AnalyzerResult
    {
        get => _analyzerResult;
        set
        {
            _analyzerResult = value;
            OnPropertyChanged();
        }
    }

    public Table? Cycles
    {
        get => _cycles;
        set
        {
            _cycles = value;
            OnPropertyChanged();
        }
    }

    public InfoPanelViewModel? InfoPanelViewModel
    {
        get => _infoPanelViewModel;
        set
        {
            if (Equals(value, _infoPanelViewModel)) return;
            _infoPanelViewModel = value;
            OnPropertyChanged();
        }
    }

    public ICommand ShowGalleryCommand { get; }


    public GraphViewModel? GraphViewModel
    {
        get => _graphViewModel;
        set
        {
            _graphViewModel = value;
            OnPropertyChanged();
        }
    }

    public bool IsLeftPanelExpanded
    {
        get => _isLeftPanelExpanded;
        set
        {
            if (_isLeftPanelExpanded == value)
            {
                return;
            }

            _isLeftPanelExpanded = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public string LoadMessage
    {
        get => _loadMessage;
        private set
        {
            _loadMessage = value;
            OnPropertyChanged();
        }
    }

    public ICommand LoadProjectCommand { get; }
    public ICommand LoadSolutionCommand { get; }
    public ICommand ImportJdepsCommand { get; }
    public ICommand SaveProjectCommand { get; }
    public ICommand GraphClearCommand { get; }
    public ICommand GraphLayoutCommand { get; }
    public ICommand ExportToDgmlCommand { get; }
    public ICommand ExportToPlantUmlCommand { get; }
    public ICommand ExportToSvgCommand { get; set; }
    public ICommand FindCyclesCommand { get; }
    public ICommand ExportToDsiCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand ExportToPngCommand { get; }
    public ICommand ShowLegendCommand { get; }
    public ICommand ExecuteAnalyzerCommand { get; set; }
    public ICommand SnapshotCommand { get; }
    public ICommand RestoreCommand { get; }


    public TreeViewModel? TreeViewModel
    {
        get => _treeViewModel;
        set
        {
            _treeViewModel = value;
            OnPropertyChanged();
        }
    }

    public AdvancedSearchViewModel? SearchViewModel
    {
        get => _searchViewModel;
        set
        {
            _searchViewModel = value;
            OnPropertyChanged();
        }
    }


    public int SelectedRightTabIndex
    {
        get => _selectedRightTabIndex;
        set
        {
            if (value == _selectedRightTabIndex)
            {
                return;
            }

            _selectedRightTabIndex = value;
            OnPropertyChanged();
        }
    }

    public bool IsCanvasHintsVisible
    {
        get => _isCanvasHintsVisible;
        set
        {
            if (_isCanvasHintsVisible == value)
            {
                return;
            }

            _isCanvasHintsVisible = value;
            OnPropertyChanged();
        }
    }

    public ICommand OpenFilterDialogCommand { get; }
    public ICommand OpenSettingsDialogCommand { get; }

    public ObservableCollection<IMetric> Metrics
    {
        set
        {
            _metrics = value;
            OnPropertyChanged();
        }
        get => _metrics;
    }

    public int SelectedLeftTabIndex
    {
        get => _selectedLeftTabIndex;
        set
        {
            if (value == _selectedLeftTabIndex) return;
            _selectedLeftTabIndex = value;
            InfoPanelViewModel?.Hide(value != InfoPanelTabIndex);
            OnPropertyChanged();
        }
    }

    public IEnumerable<IAnalyzer> Analyzers
    {
        get => _analyzerManager.All;
    }

    public ICommand CopyBitmapToClipboardCommand { get; set; }

    public bool IsGraphToolPanelVisible
    {
        get => _isGraphToolPanelVisible;
        set
        {
            if (value == _isGraphToolPanelVisible) return;
            _isGraphToolPanelVisible = value;
            OnPropertyChanged();
        }
    }

    public string Title
    {
        get
        {
            var title = Strings.AppTitle;

            if (_dirtyState == DirtyState.DirtyForceNewFile)
            {
                // Don't show filename when no longer valid
                title = title + " - " + "Refactored (model changed)";
            }
            else if (!string.IsNullOrEmpty(OpenProjectFilePath))
            {
                title = title + " - " + OpenProjectFilePath;
            }

            if (IsDirty())
            {
                title += " *";
            }

            return title;
        }
    }

    public ICommand ImportPlainTextCommand { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnUpdateProgress(object? sender, ImportStateChangedArgs e)
    {
        IsLoading = e.IsLoading;
        LoadMessage = e.ProgressMessage;
    }

    private void OnAnalyzerDataChanged(object? sender, EventArgs e)
    {
        if (_analyzerManager.IsDirty())
        {
            SetDirty(false);
        }
    }

    private bool IsDirty()
    {
        return _dirtyState != DirtyState.Saved;
    }


    private void RefreshMru()
    {
        RecentFiles.Clear();

        // Always a first browse command to avoid empty menu
        RecentFiles.Add(new Mru(Strings.Browse, LoadProjectCommand) { ImageSource = "/Resources/load_project.png" });

        foreach (var path in _userSettings.RecentFiles.Where(File.Exists).Select(f => new Mru(f, OpenRecentFileCommand)))
        {
            RecentFiles.Add(path);
        }
    }

    private void OnCopyCanvasToClipboard(FrameworkElement canvas)
    {
        try
        {
            // Get rid of the magnifier icon
            IsGraphToolPanelVisible = false;
            _exporter.ToBitmapClipboard(canvas);
        }
        finally
        {
            IsGraphToolPanelVisible = true;
        }
    }


    private void OnExecuteAnalyzer(string id)
    {
        if (_codeGraph is null)
        {
            return;
        }

        _analyzerManager.GetAnalyzer(id).Analyze(_codeGraph);
    }

    private void OnShowGallery()
    {
        if (_graphViewModel is null || _gallery is null || _codeGraph is null)
        {
            return;
        }

        var editor = new GalleryEditor
        {
            Owner = Application.Current.MainWindow
        };

        var viewModel = new GalleryEditorViewModel(_gallery,
            PreviewSession,
            AddSession,
            RemoveSession,
            LoadSession);

        var backup = _graphViewModel.GetSession();
        var hasPreviewedSession = false;

        editor.DataContext = viewModel;
        editor.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var result = editor.ShowDialog();

        if (result is false && hasPreviewedSession)
        {
            // Restore original state if previews were shown
            _graphViewModel.LoadSession(backup, false);
        }

        return;

        void RemoveSession(GraphSession session)
        {
            _gallery.Sessions.Remove(session);
            SetDirty(false);
        }

        void PreviewSession(GraphSession session)
        {
            _graphViewModel.LoadSession(session, false);
            hasPreviewedSession = true;
        }

        void LoadSession(GraphSession session)
        {
            _graphViewModel.LoadSession(session, true);
            editor.DialogResult = true;
        }

        GraphSession AddSession(string name)
        {
            var session = _graphViewModel.GetSession();
            session.Name = name;
            _gallery.AddSession(session);
            SetDirty(false);
            return session;
        }
    }

    private void SetDirty(bool forceNewFile)
    {
        if (_dirtyState == DirtyState.DirtyForceNewFile)
        {
            // We already have the most strict dirty form.
            return;
        }

        if (forceNewFile)
        {
            _dirtyState = DirtyState.DirtyForceNewFile;
        }
        else
        {
            _dirtyState = DirtyState.Dirty;
        }

        OnPropertyChanged(nameof(Title));
    }

    private void ClearDirty(string projectFilePath)
    {
        OpenProjectFilePath = projectFilePath;
        _dirtyState = DirtyState.Saved;
        OnPropertyChanged(nameof(Title));
    }

    private void OnShowLegend()
    {
        if (_openedLegendDialog == null)
        {
            _openedLegendDialog = new LegendDialog
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            _openedLegendDialog.Closed += (_, _) => _openedLegendDialog = null;
            _openedLegendDialog.Show();
        }
    }

    private void OnOpenFilterDialog()
    {
        var filterDialog = new FilterDialog(_projectExclusionFilters);
        filterDialog.ShowDialog();
    }

    private void OnOpenSettingsDialog()
    {
        var settingsDialog = new SettingsDialog(_applicationSettings)
        {
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        if (settingsDialog.ShowDialog() == true)
        {
            _applicationSettings = settingsDialog.Settings;
            ApplySettings();
        }
    }

    private void ApplySettings()
    {
        // Settings must be reloaded

        // Save settings to configuration file
        SaveSettings();
    }

    private void SaveSettings()
    {
        try
        {
            var appSettingsPath = Path.Join(Directory.GetCurrentDirectory(), "appsettings.json");
            _applicationSettings.Save(appSettingsPath);
        }
        catch (Exception ex)
        {
            // Log error or show message to user
            _ui.ShowError($"{Strings.Settings_Save_Error} {ex.Message}");
        }
    }

    private void Search()
    {
        try
        {
            IsLoading = true;

            // The search is quite fast but updating the tree view requires some time
            // So running this in a background task has no effect at all.
            TreeViewModel?.ExecuteSearch();
        }
        catch (Exception ex)
        {
            _ui.ShowError(string.Format(Strings.OperationFailed_Message, ex.Message));
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    ///     Exports the whole project to dsi.
    /// </summary>
    private void OnExportToDsi()
    {
        _exporter.ToDsi(_codeGraph);
    }

    public void HandleShowTabularData(ShowTabularDataRequest tabularDataRequest)
    {
        AnalyzerResult = tabularDataRequest.Table;
        SelectedRightTabIndex = 2;
    }

    private async void OnFindCycles()
    {
        if (_codeGraph is null)
        {
            return;
        }

        List<CycleGroup>? cycleGroups = null;
        try
        {
            IsLoading = true;
            LoadMessage = Strings.SearchingCycles_Message;
            await Task.Run(() => { cycleGroups = CycleFinder.FindCycleGroups(_codeGraph); });
        }
        catch (Exception ex)
        {
            _ui.ShowError(string.Format(Strings.OperationFailed_Message, ex.Message));
        }
        finally
        {
            LoadMessage = string.Empty;
            IsLoading = false;
        }

        if (cycleGroups != null)
        {
            _messaging.Publish(new CycleCalculationComplete(cycleGroups));
            SelectedRightTabIndex = 1;
        }
    }

    private void OnGraphLayout()
    {
        _graphViewModel?.Layout();
    }

    private void OnGraphClear()
    {
        _graphViewModel?.Clear();
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void LoadCodeGraph(CodeGraph codeGraph)
    {
        _codeGraph = codeGraph;
        _refactoringService.LoadCodeGraph(codeGraph);

        // Rebuild tree view and graph
        TreeViewModel?.LoadCodeGraph(_codeGraph);
        SearchViewModel?.LoadCodeGraph(_codeGraph);
        GraphViewModel?.LoadCodeGraph(_codeGraph);

        Cycles = null;
        AnalyzerResult = null;
        InfoPanelViewModel?.ClearQuickInfo();

        UpdateMetrics(codeGraph);
    }

    private void UpdateMetrics(CodeGraph codeGraph)
    {
        // Default output: summary of graph
        var numberOfRelationships = codeGraph.GetAllRelationships().Count();
        var outputs = new ObservableCollection<IMetric>();
        outputs.Clear();
        outputs.Add(new MetricOutput(Strings.Metric_CodeElements, codeGraph.Nodes.Count.ToString(CultureInfo.InvariantCulture)));
        outputs.Add(new MetricOutput(Strings.Metric_Relationships, numberOfRelationships.ToString(CultureInfo.InvariantCulture)));
        Metrics = outputs;
    }

    private async void OnImportSolution()
    {
        AskUserToSaveProject();

        var result = await _importer.ImportSolutionAsync(_projectExclusionFilters, _applicationSettings.IncludeExternalCode);

        if (result.IsCanceled)
        {
            return;
        }

        if (result.IsSuccess)
        {
            CompleteImport(result.Data!);
        }
    }

    private void CompleteImport(CodeGraph graph)
    {
        LoadDefaultSettings();
        LoadCodeGraph(graph);
        _gallery = new Gallery.Gallery();
        OpenProjectFilePath = string.Empty;
        SetDirty(false);
        IsCanvasHintsVisible = false;
    }

    private async void OnImportPlainText()
    {
        AskUserToSaveProject();

        var result = await _importer.ImportPlainTextAsync();
        if (result.IsSuccess)
        {
            CompleteImport(result.Data!);
        }
    }

    private async void OnImportJdeps()
    {
        AskUserToSaveProject();

        var result = await _importer.ImportJdepsAsync();
        if (result.IsSuccess)
        {
            CompleteImport(result.Data!);
        }
    }

    private void OnExportToPlantUml()
    {
        _exporter.ToPlantUml(_graphViewModel?.ExportGraph());
    }

    private void OnExportToDgml()
    {
        _exporter.ToDgml(_graphViewModel?.ExportGraph());
    }

    private void OnExportToPng(FrameworkElement? canvas)
    {
        try
        {
            // Get rid of the magnifier icon
            IsGraphToolPanelVisible = false;
            _exporter.ToPng(canvas);
        }
        finally
        {
            IsGraphToolPanelVisible = true;
        }
    }

    private void OnExportPlainText()
    {
        _exporter.ToPlainText(_graphViewModel?.ExportGraph());
    }

    /// <summary>
    ///     Not usable at the moment. It does not render subgraphs.
    /// </summary>
    private void OnExportToSvg()
    {
        if (_graphViewModel is null)
        {
            return;
        }

        _exporter.ToSvg(_graphViewModel.SaveToSvg);
    }

    private async void OnOpenRecentFile(string filePath)
    {
        await LoadProject(filePath);
    }

    private async void OnLoadProject()
    {
        await LoadProject(null);
    }

    /// <summary>
    ///     Called from command line
    /// </summary>
    public async Task LoadProjectFileAsync(string filePath)
    {
        await LoadProject(filePath);
    }

    private async Task LoadProject(string? filePath)
    {
        AskUserToSaveProject();

        Result<(string, ProjectData)> result;
        if (filePath is null)
        {
            result = await _project.LoadProjectAsync();
        }
        else
        {
            result = await _project.LoadProjectFromFileAsync(filePath);
        }

        if (result.IsCanceled)
        {
            return;
        }

        if (result.IsSuccess)
        {
            var (fileName, projectData) = result.Data;
            CompleteProjectLoaded(projectData, fileName);
        }
    }

    private void CompleteProjectLoaded(ProjectData projectData, string fileName)
    {
        RestoreProjectData(projectData);
        ClearDirty(fileName);
        _userSettings.AddRecentFile(fileName);
        RefreshMru();

        LoadMessage = string.Empty;
        IsCanvasHintsVisible = false;
        IsLoading = false;
    }


    private void LoadDefaultSettings()
    {
        if (GraphViewModel != null)
        {
            GraphViewModel.ShowFlatGraph = false;
            GraphViewModel.ShowDataFlow = false;
        }

        _projectExclusionFilters.Initialize(_applicationSettings.DefaultProjectExcludeFilter);
    }

    private void LoadSettings(Dictionary<string, string> settings)
    {
        if (GraphViewModel != null)
        {
            if (settings.TryGetValue(nameof(GraphViewModel.ShowFlatGraph), out var showFlatGraph))
            {
                GraphViewModel.ShowFlatGraph = bool.Parse(showFlatGraph);
            }

            if (settings.TryGetValue(nameof(GraphViewModel.ShowDataFlow), out var showFlow))
            {
                GraphViewModel.ShowDataFlow = bool.Parse(showFlow);
            }
        }

        if (settings.TryGetValue(nameof(ProjectExclusionRegExCollection), out var projectExcludeRegEx))
        {
            _projectExclusionFilters.Initialize(projectExcludeRegEx);
        }

        // IncludeExternals is not a configurable setting. It is global for the application.
    }

    private void OnSaveProject()
    {
        if (_codeGraph is null || _graphViewModel is null) return;

        var projectData = CollectProjectData();

        // Current path is only provided if we do not force a new file
        var currentPath = _dirtyState != DirtyState.DirtyForceNewFile
            ? _openProjectFilePath
            : null;

        var result = _project.SaveProject(projectData, currentPath);

        if (result.IsSuccess)
        {
            ClearDirty(result.Data!);
        }
    }

    /// <summary>
    ///     return true if you allow to close
    /// </summary>
    internal bool OnClosing()
    {
        AskUserToSaveProject();
        return true;
    }

    private void AskUserToSaveProject()
    {
        if (IsDirty() && _ui.AskYesNoQuestion(Strings.Save_Message, Strings.Save_Title))
        {
            OnSaveProject();
        }
    }

    public void HandleShowCycleGroupRequest(ShowCycleGroupRequest request)
    {
        GraphViewModel?.ImportCycleGroup(request.CycleGroup.CodeGraph.Clone());
        SelectedRightTabIndex = 0;
    }

    public void HandleCycleCalculationComplete(CycleCalculationComplete request)
    {
        var cycleGroups = request.CycleGroups;

        Cycles = new CycleGroupsViewModel(cycleGroups, _messaging);
        SelectedRightTabIndex = 1;
    }


    public void HandleShowPartitionsRequest(ShowPartitionsRequest request)
    {
        // We handle this in the main view model because we need the full graph.
        if (_codeGraph is null)
        {
            return;
        }

        // The request code element may originate from a graph where the children are not present!
        var originalCodeElement = _codeGraph.Nodes[request.CodeElement.Id];

        var partitions = CodeElementPartitioner.GetPartitions(_codeGraph, originalCodeElement, request.IncludeBaseClasses);

        if (partitions.Count <= 1)
        {
            _ui.ShowInfo(Strings.Partitions_NoPartitions);
            return;
        }

        // Create view model and show summary in table tab.

        var number = 1;
        var pvm = new List<PartitionViewModel>();
        foreach (var partition in partitions)
        {
            var codeElements = partition.Select(id => new CodeElementLineViewModel(_codeGraph.Nodes[id]));
            var vm = new PartitionViewModel($"Partition {number++}", codeElements);
            pvm.Add(vm);
        }

        var partitionsVm = new PartitionsViewModel(pvm);
        HandleShowTabularData(new ShowTabularDataRequest(partitionsVm));
    }

    public void HandleCodeGraphRefactored(CodeGraphRefactored message)
    {
        _searchViewModel?.HandleCodeGraphRefactored(message);
        _graphViewModel?.HandleCodeGraphRefactored(message);
        _treeViewModel?.HandleCodeGraphRefactored(message);
        _gallery?.HandleCodeGraphRefactored(message);

        // Brute force
        // LoadCodeGraph(_codeGraph);

        // Maybe not valid anymore
        Cycles = null;
        AnalyzerResult = null;
        InfoPanelViewModel?.ClearQuickInfo();

        UpdateMetrics(message.Graph);

        // Force new file
        SetDirty(true);
    }


    private ProjectData CollectProjectData()
    {
        var projectData = new ProjectData();
        projectData.SetCodeGraph(_codeGraph!);
        projectData.SetGallery(_gallery ?? new Gallery.Gallery());
        projectData.Settings[nameof(GraphViewModel.ShowFlatGraph)] = _graphViewModel!.ShowFlatGraph.ToString();
        projectData.Settings[nameof(GraphViewModel.ShowDataFlow)] = _graphViewModel.ShowDataFlow.ToString();
        projectData.Settings[nameof(ProjectExclusionRegExCollection)] = _projectExclusionFilters.ToString();
        projectData.AnalyzerData = _analyzerManager.CollectAnalyzerData();
        return projectData;
    }

    private void OnSnapshot()
    {
        if (_codeGraph is null || _graphViewModel is null)
        {
            return;
        }

        try
        {
            // Create a snapshot by capturing the current state (similar to OnSaveProject)
            var projectData = CollectProjectData();
            _project.CreateSnapshot(projectData);
        }
        catch (Exception ex)
        {
            _ui.ShowError($"{Strings.Snapshot_Failed}: {ex.Message}");
        }
    }

    private void OnRestore()
    {
        // Note we do not touch the dirty flags when restoring.
        // If you refactored a new file to save is already requested.
        // If not, nothing you did with the application triggered the dirty flag.
        _project.RestoreSnapshot(RestoreProjectData);
    }

    private void RestoreProjectData(ProjectData projectData)
    {
        LoadSettings(projectData.Settings);
        LoadCodeGraph(projectData.GetCodeGraph());
        _gallery = projectData.GetGallery();

        // Restore analyzer data
        _analyzerManager.RestoreAnalyzerData(projectData.AnalyzerData);
    }

    
    
    
    
    
    public void ClearQuickInfo()
    {
        _infoPanelViewModel?.ClearQuickInfo();
        _graphViewModel.ClearQuickInfo();
    }
}