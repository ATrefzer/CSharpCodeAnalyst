using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using CodeParser.Analysis.Cycles;
using CodeParser.Analysis.EventRegistration;
using CodeParser.Analysis.Shared;
using CodeParser.Extensions;
using CodeParser.Parser;
using CodeParser.Parser.Config;
using Contracts.Common;
using Contracts.Graph;
using CSharpCodeAnalyst.Areas.ResultArea;
using CSharpCodeAnalyst.Areas.TableArea;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.Configuration;
using CSharpCodeAnalyst.CycleArea;
using CSharpCodeAnalyst.Exploration;
using CSharpCodeAnalyst.Exports;
using CSharpCodeAnalyst.Filter;
using CSharpCodeAnalyst.Gallery;
using CSharpCodeAnalyst.GraphArea;
using CSharpCodeAnalyst.Help;
using CSharpCodeAnalyst.Import;
using CSharpCodeAnalyst.InfoPanel;
using CSharpCodeAnalyst.MetricArea;
using CSharpCodeAnalyst.Project;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.SearchArea;
using CSharpCodeAnalyst.TreeArea;
using Microsoft.Win32;
using Prism.Commands;

namespace CSharpCodeAnalyst;



internal class MainViewModel : INotifyPropertyChanged
{
    private const int InfoPanelTabIndex = 2;
    private const int TableTabIndex = 1;

    private readonly int _maxDegreeOfParallelism;
    private readonly MessageBus _messaging;

    private readonly ProjectExclusionRegExCollection _projectExclusionFilters;
    private ApplicationSettings _applicationSettings;
    private CodeGraph? _codeGraph;
    private Gallery.Gallery? _gallery;

    private GraphViewModel? _graphViewModel;
    private InfoPanelViewModel? _infoPanelViewModel;

    private bool _isCanvasHintsVisible = true;
    private bool _isLeftPanelExpanded = true;
    private bool _isLoading;
    private bool _isSaved = true;

    private string _loadMessage;
    private ObservableCollection<IMetric> _metrics = [];
    private LegendDialog? _openedLegendDialog;
    private SearchViewModel? _searchViewModel;


    private int _selectedLeftTabIndex;
    private int _selectedRightTabIndex;
    private TableViewModel? _tableViewModel;
    private TreeViewModel? _treeViewModel;

    // TODO
    private IPluginTableData _currentPluginData = new SamplePersonTableData();

    public IPluginTableData CurrentPluginData
    {
        get => _currentPluginData;
        set
        {
            _currentPluginData = value;
            OnPropertyChanged();
        }
    }

    internal MainViewModel(MessageBus messaging, ApplicationSettings settings)
    {
        // Initialize settings
        _applicationSettings = settings;

        // Apply settings
        _projectExclusionFilters = new ProjectExclusionRegExCollection();
        _maxDegreeOfParallelism = _applicationSettings.MaxDegreeOfParallelism;

        _projectExclusionFilters.Initialize(_applicationSettings.DefaultProjectExcludeFilter, ";");

        _messaging = messaging;
        _gallery = new Gallery.Gallery();
        SearchCommand = new DelegateCommand(Search);
        LoadSolutionCommand = new DelegateCommand(OnLoadSolution);
        ImportJdepsCommand = new DelegateCommand(OnImportJdeps);
        LoadProjectCommand = new DelegateCommand(OnLoadProject);
        SaveProjectCommand = new DelegateCommand(OnSaveProject);
        GraphClearCommand = new DelegateCommand(OnGraphClear);
        GraphLayoutCommand = new DelegateCommand(OnGraphLayout);
        FindCyclesCommand = new DelegateCommand(OnFindCycles);
        FindEventImbalancesCommand = new DelegateCommand(OnFindEventImbalances);
        ShowGalleryCommand = new DelegateCommand(OnShowGallery);
        ShowLegendCommand = new DelegateCommand(OnShowLegend);
        OpenFilterDialogCommand = new DelegateCommand(OnOpenFilterDialog);
        OpenSettingsDialogCommand = new DelegateCommand(OnOpenSettingsDialog);
        ExportToDgmlCommand = new DelegateCommand(OnExportToDgml);
        ExportToPlantUmlCommand = new DelegateCommand(OnExportToPlantUml);
        ExportToSvgCommand = new DelegateCommand(OnExportToSvg);
        ExportToPngCommand = new DelegateCommand<FrameworkElement>(OnExportToPng);
        ExportToDsiCommand = new DelegateCommand(OnExportToDsi);

        _loadMessage = string.Empty;
    }


    public InfoPanelViewModel? InfoPanelViewModel
    {
        get => _infoPanelViewModel;
        set
        {
            if (Equals(value, _infoPanelViewModel)) return;
            _infoPanelViewModel = value;
            OnPropertyChanged(nameof(InfoPanelViewModel));
        }
    }

    public ICommand ShowGalleryCommand { get; }


    public TableViewModel? TableViewModel
    {
        get => _tableViewModel;
        set
        {
            if (Equals(value, _tableViewModel))
            {
                return;
            }

            _tableViewModel = value;
            OnPropertyChanged(nameof(TableViewModel));
        }
    }

    public GraphViewModel? GraphViewModel
    {
        get => _graphViewModel;
        set
        {
            _graphViewModel = value;
            OnPropertyChanged(nameof(GraphViewModel));
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
            OnPropertyChanged(nameof(IsLeftPanelExpanded));
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged(nameof(IsLoading));
        }
    }

    public string LoadMessage
    {
        get => _loadMessage;
        set
        {
            _loadMessage = value;
            OnPropertyChanged(nameof(LoadMessage));
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


    public TreeViewModel? TreeViewModel
    {
        get => _treeViewModel;
        set
        {
            _treeViewModel = value;
            OnPropertyChanged(nameof(TreeViewModel));
        }
    }

    public SearchViewModel? SearchViewModel
    {
        get => _searchViewModel;
        set
        {
            _searchViewModel = value;
            OnPropertyChanged(nameof(SearchViewModel));
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
            OnPropertyChanged(nameof(SelectedRightTabIndex));
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
            OnPropertyChanged(nameof(IsCanvasHintsVisible));
        }
    }

    public ICommand OpenFilterDialogCommand { get; }
    public ICommand OpenSettingsDialogCommand { get; }

    public ObservableCollection<IMetric> Metrics
    {
        set
        {
            _metrics = value;
            OnPropertyChanged(nameof(Metrics));
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
            OnPropertyChanged(nameof(SelectedLeftTabIndex));
        }
    }

    public ICommand FindEventImbalancesCommand { get; set; }


    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnShowGallery()
    {
        if (_graphViewModel is null || _gallery is null || _codeGraph is null)
        {
            return;
        }

        var editor = new GalleryEditor();
        var viewModel = new GalleryEditorViewModel(_gallery,
            PreviewSession,
            AddSession,
            RemoveSession,
            LoadSession);

        var backup = _graphViewModel.GetSession();
        GraphSession? preview = null;

        editor.DataContext = viewModel;
        editor.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var result = editor.ShowDialog();

        if (result is false && !ReferenceEquals(backup, preview))
        {
            // Restore original state if previews were shown
            _graphViewModel.LoadSession(backup, false);
        }

        void RemoveSession(GraphSession session)
        {
            _gallery.Sessions.Remove(session);
            _isSaved = false;
        }

        void PreviewSession(GraphSession session)
        {
            _graphViewModel.LoadSession(session, false);
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
            _isSaved = false;
            return session;
        }
    }

    private void OnShowLegend()
    {
        if (_openedLegendDialog == null)
        {
            _openedLegendDialog = new LegendDialog();
            _openedLegendDialog.Owner = Application.Current.MainWindow;
            _openedLegendDialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
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
        var settingsDialog = new SettingsDialog(_applicationSettings);
        settingsDialog.Owner = Application.Current.MainWindow;
        settingsDialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
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
            var appSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
            _applicationSettings.Save(appSettingsPath);
        }
        catch (Exception ex)
        {
            // Log error or show message to user
            MessageBox.Show($"{Strings.Settings_Save_Error} {ex.Message}", Strings.Error_Title,
                MessageBoxButton.OK, MessageBoxImage.Warning);
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
            var message = string.Format(Strings.OperationFailed_Message, ex.Message);
            MessageBox.Show(message, Strings.Error_Title, MessageBoxButton.OK,
                MessageBoxImage.Error);
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
        Export.ToDsi(_codeGraph);
    }

    private static void RunDsiViewer(string filePath)
    {
        var executablePath = @"ExternalApplications\\DsmSuite.DsmViewer.View.exe";

        var process = new Process();
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = filePath,
            UseShellExecute = false,
            RedirectStandardOutput = false,
            CreateNoWindow = true
        };

        process.StartInfo = startInfo;
        process.Start();
    }

    private void OnFindEventImbalances()
    {
        if (_codeGraph is null)
        {
            return;
        }

        var imbalances = EventRegistrationAnalyzer.FindImbalances(_codeGraph);

        if (imbalances.Count == 0)
        {
            MessageBox.Show("No event handler registration / un-registration imbalances found");
            return;
        }

        HandleShowEventImbalancesRequest(new ShowEventImbalancesRequest(imbalances));
    }


    public void HandleShowEventImbalancesRequest(ShowEventImbalancesRequest request)
    {
        var vm = new EventImbalancesViewModel(request.Imbalances);
        TableViewModel = vm;
        SelectedRightTabIndex = TableTabIndex;
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
            var message = string.Format(Strings.OperationFailed_Message, ex.Message);
            MessageBox.Show(message, Strings.Error_Title, MessageBoxButton.OK,
                MessageBoxImage.Error);
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

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }



    private async Task<(CodeGraph, IParserDiagnostics)> ImportSolutionAsync(string solutionPath)
    {
        LoadMessage = "Loading ...";
        var parser = new Parser(new ParserConfig(_projectExclusionFilters, _maxDegreeOfParallelism));
        parser.Progress.ParserProgress += OnProgress;
        var graph = await parser.ParseSolution(solutionPath).ConfigureAwait(true);

        parser.Progress.ParserProgress -= OnProgress;
        return (graph, parser.Diagnostics);
    }

    private void LoadCodeGraph(CodeGraph codeGraph)
    {
        _codeGraph = codeGraph;

        // Rebuild tree view and graph
        TreeViewModel?.LoadCodeGraph(_codeGraph);
        SearchViewModel?.LoadCodeGraph(_codeGraph);
        GraphViewModel?.LoadCodeGraph(_codeGraph);
        TableViewModel?.Clear();
        InfoPanelViewModel?.Clear();

        // Default output: summary of graph
        var numberOfRelationships = codeGraph.GetAllRelationships().Count();
        var outputs = new ObservableCollection<IMetric>();
        outputs.Clear();
        outputs.Add(new MetricOutput("# Code elements", codeGraph.Nodes.Count.ToString(CultureInfo.InvariantCulture)));
        outputs.Add(new MetricOutput("# Relationships", numberOfRelationships.ToString(CultureInfo.InvariantCulture)));
        Metrics = outputs;
    }

    private async void OnLoadSolution()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Solution files (*.sln)|*.sln",
            Title = "Select a solution file"
        };

        if (openFileDialog.ShowDialog() != true)
        {
            return;
        }

        var solutionPath = openFileDialog.FileName;

        try
        {
            IsLoading = true;

            var codeGraph = await LoadSolutionAsync(solutionPath);
            LoadDefaultSettings();
            LoadCodeGraph(codeGraph);
            _gallery = new Gallery.Gallery();
            _isSaved = false;
        }
        catch (Exception ex)
        {
            var message = string.Format(Strings.OperationFailed_Message, ex.Message);
            MessageBox.Show(message, Strings.Error_Title, MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsCanvasHintsVisible = false;
            IsLoading = false;
        }
    }

    private async Task<CodeGraph> LoadSolutionAsync(string solutionPath)
    {
        var (codeGraph, diagnostics) = await Task.Run(async () => await ImportSolutionAsync(solutionPath));

        var failures = diagnostics.FormatFailures();
        if (!string.IsNullOrEmpty(failures))
        {
            var failureText = Strings.Parser_FailureHeader + failures;
            MessageBox.Show(failureText, Strings.Error_Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        return codeGraph;
    }

    private void OnImportJdeps()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            Title = "Select jdeps output file"
        };

        if (openFileDialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            IsLoading = true;
            LoadMessage = "Importing jdeps data...";

            var importer = new JdepsImporter();
            var codeGraph = importer.ImportFromFile(openFileDialog.FileName);

            LoadCodeGraph(codeGraph);

            // Imported a new jdeps file
            _isSaved = false;
        }
        catch (Exception ex)
        {
            var message = string.Format(Strings.OperationFailed_Message, ex.Message);
            MessageBox.Show(message, Strings.Error_Title, MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsCanvasHintsVisible = false;
            IsLoading = false;
            LoadMessage = string.Empty;
        }
    }
    private void OnProgress(object? sender, ParserProgressArg e)
    {
        LoadMessage = e.Message;
    }
    private void OnExportToPlantUml()
    {
        Export.ToPlantUml(_graphViewModel?.ExportGraph());
    }

    private void OnExportToDgml()
    {
        Export.ToDgml(_graphViewModel?.ExportGraph());
    }

    private void OnExportToPng(FrameworkElement? canvas)
    {
        Export.ToPng(canvas);
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

        Export.ToSvg(_graphViewModel.SaveToSvg);
    }

    private async void OnLoadProject()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            Title = "Load Project"
        };

        if (openFileDialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            LoadMessage = "Loading ...";
            IsLoading = true;

            var fileName = openFileDialog.FileName;
            var (codeGraph, projectData) = await Task.Run(() => LoadProject(fileName));

            LoadSettings(projectData.Settings);
            LoadCodeGraph(codeGraph);
            _gallery = projectData.GetGallery();
            _isSaved = true;
        }
        catch (Exception ex)
        {
            var message = string.Format(Strings.OperationFailed_Message, ex.Message);
            MessageBox.Show(message, Strings.Error_Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            LoadMessage = string.Empty;
            IsCanvasHintsVisible = false;
            IsLoading = false;
        }
    }

    private void LoadDefaultSettings()
    {
        if (GraphViewModel != null)
        {
            GraphViewModel.ShowFlatGraph = false;
            GraphViewModel.ShowDataFlow = false;
        }

        _projectExclusionFilters.Initialize(_applicationSettings.DefaultProjectExcludeFilter, ";");
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
            _projectExclusionFilters.Initialize(projectExcludeRegEx, ";");
        }
    }

    private (CodeGraph codeGraph, ProjectData projectData) LoadProject(string fileName)
    {
        var json = File.ReadAllText(fileName);
        var options = new JsonSerializerOptions
        {
            ReferenceHandler = ReferenceHandler.Preserve
        };
        var projectData = JsonSerializer.Deserialize<ProjectData>(json, options);
        if (projectData is null)
        {
            throw new NullReferenceException();
        }

        var codeGraph = projectData.GetCodeGraph();

        return (codeGraph, projectData);
    }

    private void OnSaveProject()
    {
        if (_codeGraph is null || _graphViewModel is null)
        {
            return;
        }

        var saveFileDialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            Title = "Save Project"
        };

        if (saveFileDialog.ShowDialog() != true)
        {
            return;
        }

        var projectData = new ProjectData();
        projectData.SetCodeGraph(_codeGraph);
        projectData.SetGallery(_gallery ?? new Gallery.Gallery());
        projectData.Settings[nameof(GraphViewModel.ShowFlatGraph)] = _graphViewModel.ShowFlatGraph.ToString();
        projectData.Settings[nameof(GraphViewModel.ShowDataFlow)] = _graphViewModel.ShowDataFlow.ToString();
        projectData.Settings[nameof(ProjectExclusionRegExCollection)] = _projectExclusionFilters.ToString();

        // Add other settings here

        var options = new JsonSerializerOptions
        {
            // The file gets quite large, so we don't want to have it indented.
            WriteIndented = false
        };
        var json = JsonSerializer.Serialize(projectData, options);
        File.WriteAllText(saveFileDialog.FileName, json);
        _isSaved = true;
    }



    public void HandleDeleteFromModel(DeleteFromModelRequest request)
    {
        if (_codeGraph is null)
        {
            return;
        }

        _codeGraph.RemoveCodeElementAndAllChildren(request.Id);
        LoadCodeGraph(_codeGraph);
        _isSaved = false;
    }

    /// <summary>
    ///     return true if you allow to close
    /// </summary>
    internal bool OnClosing()
    {
        if (!_isSaved)
        {
            if (MessageBox.Show(Strings.Save_Message, Strings.Save_Title,
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                OnSaveProject();
            }
        }

        return true;
    }

    public void HandleShowCycleGroupRequest(ShowCycleGroupRequest request)
    {
        GraphViewModel?.ImportCycleGroup(request.CycleGroup.CodeGraph.Clone());
        SelectedRightTabIndex = 0;
    }

    public void HandleCycleCalculationComplete(CycleCalculationComplete request)
    {
        var cycleGroups = request.CycleGroups;

        TableViewModel = new CycleGroupsViewModel(cycleGroups, _messaging);
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

        var partitioner = new CodeElementPartitioner();
        var partitions = partitioner.GetPartitions(_codeGraph, originalCodeElement, request.IncludeBaseClasses);

        if (partitions.Count <= 1)
        {
            MessageBox.Show(Strings.Partitions_NoPartitions, Strings.Information_Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Create view model and show summary in table tab.


        var partitionsVm = new PartitionsViewModel();
        var number = 1;
        foreach (var partition in partitions)
        {
            var codeElements = partition.Select(id => new CodeElementLineViewModel(_codeGraph.Nodes[id]));
            var vm = new PartitionViewModel($"Partition {number++}", codeElements);
            partitionsVm.Partitions.Add(vm);
        }

        TableViewModel = partitionsVm;
        SelectedRightTabIndex = 1;
    }
}
