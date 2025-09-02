using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using CodeParser.Analysis.Cycles;
using CodeParser.Analysis.Shared;
using CodeParser.Export;
using CodeParser.Extensions;
using CodeParser.Parser;
using CodeParser.Parser.Config;
using Contracts.Common;
using Contracts.Graph;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.Configuration;
using CSharpCodeAnalyst.CycleArea;
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
    private readonly int _maxDegreeOfParallelism;
    private readonly MessageBus _messaging;

    private readonly ProjectExclusionRegExCollection _projectExclusionFilters;
    private ApplicationSettings _applicationSettings;
    private CodeGraph? _codeGraph;
    private CycleSummaryViewModel? _cycleSummaryViewModel;
    private Gallery.Gallery? _gallery;

    private GraphViewModel? _graphViewModel;

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

    private TreeViewModel? _treeViewModel;

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
        LoadSolutionCommand = new DelegateCommand(LoadSolution);
        ImportJdepsCommand = new DelegateCommand(ImportJdeps);
        LoadProjectCommand = new DelegateCommand(LoadProject);
        SaveProjectCommand = new DelegateCommand(SaveProject);
        GraphClearCommand = new DelegateCommand(GraphClear);
        GraphLayoutCommand = new DelegateCommand(GraphLayout);
        ExportToDgmlCommand = new DelegateCommand(ExportToDgml);
        ExportToSvgCommand = new DelegateCommand(ExportToSvg);
        FindCyclesCommand = new DelegateCommand(FindCycles);
        ShowGalleryCommand = new DelegateCommand(ShowGallery);
        ExportToDsiCommand = new DelegateCommand(ExportToDsi);
        ShowLegendCommand = new DelegateCommand(ShowLegend);
        OpenFilterDialogCommand = new DelegateCommand(OpenFilterDialog);
        OpenSettingsDialogCommand = new DelegateCommand(OpenSettingsDialog);
        ExportToPngCommand = new DelegateCommand<FrameworkElement>(ExportToPng);

        CopyToExplorerGraphCommand = new DelegateCommand<CycleGroupViewModel>(CopyToExplorerGraph);

        _loadMessage = string.Empty;
    }

    public ICommand ShowGalleryCommand { get; }


    public CycleSummaryViewModel? CycleSummaryViewModel
    {
        get => _cycleSummaryViewModel;
        set
        {
            if (Equals(value, _cycleSummaryViewModel))
            {
                return;
            }

            _cycleSummaryViewModel = value;
            OnPropertyChanged(nameof(CycleSummaryViewModel));
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
    public ICommand ExportToSvgCommand { get; set; }
    public ICommand CopyToExplorerGraphCommand { get; set; }
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

    public InfoPanelViewModel InfoPanelViewModel { get; set; }

    public int SelectedLeftTabIndex
    {
        get => _selectedLeftTabIndex;
        set
        {
            if (value == _selectedLeftTabIndex) return;
            _selectedLeftTabIndex = value;
            InfoPanelViewModel.Hide(value != 2);
            OnPropertyChanged(nameof(SelectedLeftTabIndex));
        }
    }


    public event PropertyChangedEventHandler? PropertyChanged;

    private void ShowGallery()
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

        if (result is false && ReferenceEquals(backup, preview) is false)
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

    private void ShowLegend()
    {
        if (_openedLegendDialog == null)
        {
            _openedLegendDialog = new LegendDialog();
            _openedLegendDialog.Owner = Application.Current.MainWindow;
            _openedLegendDialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            _openedLegendDialog.Closed += (sender, e) => _openedLegendDialog = null;
            _openedLegendDialog.Show();
        }
    }

    private void OpenFilterDialog()
    {
        var filterDialog = new FilterDialog(_projectExclusionFilters);
        filterDialog.ShowDialog();
    }

    private void OpenSettingsDialog()
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
    private async void ExportToDsi()
    {
        if (_codeGraph is null)
        {
            return;
        }

        try
        {
            IsLoading = true;

            var exporter = new DsiExport();

            var filePath = await Task.Run(() =>
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var appName = Assembly.GetExecutingAssembly().GetName().Name ?? "CodeAnalyst";
                var directory = Path.Combine(appDataPath, appName);
                Directory.CreateDirectory(directory);
                var fileName = Path.GetRandomFileName() + ".dsi";
                var filePath = Path.Combine(directory, fileName);

                exporter.Export(filePath, _codeGraph);
                return filePath;
            });

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

    private async void FindCycles()
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

    private void GraphLayout()
    {
        _graphViewModel?.Layout();
    }

    private void GraphClear()
    {
        _graphViewModel?.Clear();
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async Task LoadAndAnalyzeSolution(string solutionPath)
    {
        try
        {
            IsLoading = true;

            var (codeGraph, diagnostics) = await Task.Run(async () => await LoadAsync(solutionPath));

            var failures = diagnostics.FormatFailures();
            if (string.IsNullOrEmpty(failures) is false)
            {
                var failureText = Strings.Parser_FailureHeader + failures;
                MessageBox.Show(failureText, Strings.Error_Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            LoadCodeGraph(codeGraph);

            // Imported a new solution
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

    private async Task<(CodeGraph, IParserDiagnostics)> LoadAsync(string solutionPath)
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
        CycleSummaryViewModel?.Clear();
        InfoPanelViewModel?.Clear();

        // Default output: summary of graph
        var numberOfRelationships = codeGraph.GetAllRelationships().Count();
        var outputs = new ObservableCollection<IMetric>();
        outputs.Clear();
        outputs.Add(new MetricOutput("# Code elements", codeGraph.Nodes.Count.ToString(CultureInfo.InvariantCulture)));
        outputs.Add(new MetricOutput("# Relationships", numberOfRelationships.ToString(CultureInfo.InvariantCulture)));
        Metrics = outputs;
    }

    private async void LoadSolution()
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
        await LoadAndAnalyzeSolution(solutionPath);
    }

    private void ImportJdeps()
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

    private void ExportToDgml()
    {
        if (_graphViewModel is null)
        {
            return;
        }

        var saveFileDialog = new SaveFileDialog
        {
            Filter = "DGML files (*.dgml)|*.dgml",
            Title = "Export to DGML"
        };

        if (saveFileDialog.ShowDialog() != true)
        {
            return;
        }

        var codeStructure = _graphViewModel.ExportGraph();
        var exporter = new DgmlExport();
        exporter.Export(saveFileDialog.FileName, codeStructure);
    }


    private void ExportToPng(FrameworkElement canvas)
    {
        if (_graphViewModel is null)
        {
            return;
        }

        var saveFileDialog = new SaveFileDialog
        {
            Filter = "PNG files (*.png)|*.png",
            Title = "Export to DGML"
        };

        if (saveFileDialog.ShowDialog() != true)
        {
            return;
        }

        ImageWriter.SaveToPng(canvas, saveFileDialog.FileName);
    }

    /// <summary>
    ///     Not usable at the moment. It does not render subgraphs.
    /// </summary>
    private void ExportToSvg()
    {
        if (_graphViewModel is null)
        {
            return;
        }

        var saveFileDialog = new SaveFileDialog
        {
            Filter = "SVG files (*.svg)|*.svg",
            Title = "Export to SVG"
        };

        if (saveFileDialog.ShowDialog() != true)
        {
            return;
        }

        using (var stream = new FileStream(saveFileDialog.FileName, FileMode.Create))
        {
            _graphViewModel.SaveToSvg(stream);
        }
    }


    private void LoadProject()
    {
        try
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

            var json = File.ReadAllText(openFileDialog.FileName);
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


            // Load settings
            if (projectData.Settings.TryGetValue(nameof(InfoPanelViewModel.IsInfoPanelVisible), out var isInfoPanelVisibleString))
            {
                InfoPanelViewModel.IsInfoPanelVisible = bool.Parse(isInfoPanelVisibleString);
            }

            if (GraphViewModel != null)

            {
                if (projectData.Settings.TryGetValue(nameof(GraphViewModel.ShowFlatGraph), out var showFlatGraph))
                {
                    GraphViewModel.ShowFlatGraph = bool.Parse(showFlatGraph);
                }

                if (projectData.Settings.TryGetValue(nameof(GraphViewModel.ShowDataFlow), out var showFlow))
                {
                    GraphViewModel.ShowDataFlow = bool.Parse(showFlow);
                }
            }

            if (projectData.Settings.TryGetValue(nameof(ProjectExclusionRegExCollection), out var projectExcludeRegEx))
            {
                _projectExclusionFilters.Initialize(projectExcludeRegEx, ";");
            }

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
            IsCanvasHintsVisible = false;
        }
    }

    private void SaveProject()
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
        projectData.Settings[nameof(InfoPanelViewModel.IsInfoPanelVisible)] = InfoPanelViewModel.IsInfoPanelVisible.ToString();
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

    private void CopyToExplorerGraph(CycleGroupViewModel vm)
    {
        var graph = vm.CycleGroup.CodeGraph;

        GraphViewModel?.ImportCycleGroup(graph.Clone());

        SelectedRightTabIndex = 0;
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
        if (_isSaved is false)
        {
            if (MessageBox.Show(Strings.Save_Message, Strings.Save_Title,
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                SaveProject();
            }
        }

        return true;
    }
}