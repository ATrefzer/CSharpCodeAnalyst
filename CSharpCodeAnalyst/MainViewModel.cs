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
using Contracts.Graph;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.Configuration;
using CSharpCodeAnalyst.CycleArea;
using CSharpCodeAnalyst.Exports;
using CSharpCodeAnalyst.Filter;
using CSharpCodeAnalyst.GraphArea;
using CSharpCodeAnalyst.Help;
using CSharpCodeAnalyst.MetricArea;
using CSharpCodeAnalyst.Project;
using CSharpCodeAnalyst.TreeArea;
using Microsoft.Win32;
using Prism.Commands;

namespace CSharpCodeAnalyst;

internal class MainViewModel : INotifyPropertyChanged
{
    private readonly MessageBus _messaging;

    private readonly ProjectExclusionRegExCollection _projectExclusionFilters;
    private readonly int _warningCodeElementLimitForCycle;
    private CodeGraph? _codeGraph;
    private CycleSummaryViewModel? _cycleSummaryViewModel;

    private GraphViewModel? _graphViewModel;

    private bool _isCanvasHintsVisible = true;

    private bool _isInfoPanelVisible;

    private bool _isLeftPanelExpanded = true;

    private bool _isLoading;

    private bool _isSaved = true;

    private string _loadMessage;
    private ObservableCollection<IMetric> _metrics = [];

    private List<QuickInfo> _quickInfo = QuickInfoFactory.NoInfoProviderRegistered;
    private int _selectedTabIndex;

    private TreeViewModel? _treeViewModel;

    internal MainViewModel(MessageBus messaging, ApplicationSettings? settings)
    {
        _projectExclusionFilters = new ProjectExclusionRegExCollection();

        if (settings != null)
        {
            _isInfoPanelVisible = settings.DefaultShowQuickHelp;
            _projectExclusionFilters.Initialize(settings.DefaultProjectExcludeFilter, ";");
            _warningCodeElementLimitForCycle = settings.WarningCodeElementLimitForCycle;
        }

        _messaging = messaging;
        SearchCommand = new DelegateCommand(Search);
        LoadSolutionCommand = new DelegateCommand(LoadSolution);
        LoadProjectCommand = new DelegateCommand(LoadProject);
        SaveProjectCommand = new DelegateCommand(SaveProject);
        GraphClearCommand = new DelegateCommand(GraphClear);
        GraphResetZoomCommand = new DelegateCommand(GraphResetZoom);
        ExportToDgmlCommand = new DelegateCommand(ExportToDgml);
        ExportToSvgCommand = new DelegateCommand(ExportToSvg);
        FindCyclesCommand = new DelegateCommand(FindCycles);
        ExportToDsiCommand = new DelegateCommand(ExportToDsi);
        OpenFilterDialogCommand = new DelegateCommand(OpenFilterDialog);
        ExportToPngCommand = new DelegateCommand<FrameworkElement>(ExportToPng);
        OpenSourceLocationCommand = new DelegateCommand<SourceLocation>(OpenSourceLocation);
        CopyToExplorerGraphCommand = new DelegateCommand<CycleGroupViewModel>(CopyToExplorerGraph);

        _loadMessage = string.Empty;
    }

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

    public List<QuickInfo> QuickInfo
    {
        get => _quickInfo;
        set
        {
            if (Equals(value, _quickInfo))
            {
                return;
            }

            _quickInfo = value;
            OnPropertyChanged(nameof(QuickInfo));
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

    public bool IsInfoPanelVisible
    {
        get => _isInfoPanelVisible && _selectedTabIndex == 0;
        set
        {
            if (_isInfoPanelVisible == value)
            {
                return;
            }

            _isInfoPanelVisible = value;
            OnPropertyChanged(nameof(IsInfoPanelVisible));
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

    public ICommand SaveProjectCommand { get; }
    public ICommand GraphClearCommand { get; }
    public ICommand GraphResetZoomCommand { get; }

    public ICommand ExportToDgmlCommand { get; }

    public ICommand ExportToSvgCommand { get; set; }

    public ICommand CopyToExplorerGraphCommand { get; set; }

    public ICommand FindCyclesCommand { get; }
    public ICommand ExportToDsiCommand { get; }
    public ICommand OpenSourceLocationCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand ExportToPngCommand { get; }


    public TreeViewModel? TreeViewModel
    {
        get => _treeViewModel;
        set
        {
            _treeViewModel = value;
            OnPropertyChanged(nameof(TreeViewModel));
        }
    }


    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (value == _selectedTabIndex)
            {
                return;
            }

            _selectedTabIndex = value;
            OnPropertyChanged(nameof(SelectedTabIndex));
            OnPropertyChanged(nameof(IsInfoPanelVisible));
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

    public ObservableCollection<IMetric> Metrics
    {
        set
        {
            _metrics = value;
            OnPropertyChanged(nameof(Metrics));
        }
        get => _metrics;
    }


    public event PropertyChangedEventHandler? PropertyChanged;

    private void OpenFilterDialog()
    {
        var filterDialog = new FilterDialog(_projectExclusionFilters);
        filterDialog.ShowDialog();
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
            MessageBox.Show($"Error searching: {ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }


    private void OpenSourceLocation(SourceLocation? location)
    {
        if (location is null)
        {
            return;
        }

        var process = new Process();
        var startInfo = new ProcessStartInfo
        {
            FileName = "\"C:\\Program Files\\Notepad++\\notepad++.exe\"",
            Arguments = $"-n{location.Line} -c{location.Column} \"{location.File}\"",
            UseShellExecute = false,
            RedirectStandardOutput = false,
            CreateNoWindow = true
        };

        try
        {
            process.StartInfo = startInfo;
            process.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error opening source file: {ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public void HandleUpdateQuickInfo(QuickInfoUpdate quickInfoUpdate)
    {
        // May come from any view
        QuickInfo = quickInfoUpdate.QuickInfo;
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
            MessageBox.Show($"Error converting solution to dsi file: {ex.Message}", "Error", MessageBoxButton.OK,
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
            LoadMessage = "Searching Cycles ...";
            await Task.Run(() => { cycleGroups = CycleFinder.FindCycleGroups(_codeGraph); });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error finding cycles: {ex.Message}", "Error", MessageBoxButton.OK,
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
            SelectedTabIndex = 1;
        }
    }

    private void GraphResetZoom()
    {
        _graphViewModel?.ResetZoom();
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

            var codeGraph = await Task.Run(async () => await LoadAsync(solutionPath));

            LoadCodeGraph(codeGraph);
            
            // Debug output for the parser result.
            //DgmlHierarchyExport.Export(@"d:\test_hierarchy.dgml", codeStructure);
            //DgmlDependencyExport.Export(@"d:\test_dependency.dgml", codeStructure);

            // Imported a new solution
            _isSaved = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading solution: {ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsCanvasHintsVisible = false;
            IsLoading = false;
        }
    }

    private async Task<CodeGraph> LoadAsync(string solutionPath)
    {
        LoadMessage = "Loading ...";
        var parser = new Parser(new ParserConfig(_projectExclusionFilters));
        parser.ParserProgress += OnProgress;
        var graph = await parser.ParseSolution(solutionPath).ConfigureAwait(true);

        parser.ParserProgress -= OnProgress;
        return graph;
    }


    private void LoadCodeGraph(CodeGraph codeGraph)
    {
        _codeGraph = codeGraph;

        // Rebuild tree view and graph
        if (TreeViewModel != null)
        {
            TreeViewModel.LoadCodeGraph(_codeGraph);
        }

        if (GraphViewModel != null)
        {
            GraphViewModel.LoadCodeGraph(_codeGraph);
        }

        if (CycleSummaryViewModel != null)
        {
            CycleSummaryViewModel.Clear();
        }

        // Default output: summary of graph
        var numberOfDependencies = codeGraph.Nodes.Values.SelectMany(n => n.Dependencies).Count();
        var outputs = new ObservableCollection<IMetric>();
        outputs.Clear();
        outputs.Add(new MetricOutput("# Code elements", codeGraph.Nodes.Count.ToString(CultureInfo.InvariantCulture)));
        outputs.Add(new MetricOutput("# Dependencies", numberOfDependencies.ToString(CultureInfo.InvariantCulture)));
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

    private void OnProgress(object? sender, ParserProgressArg e)
    {
        LoadMessage = $"Loading ... (already parsed {e.NumberOfParsedElements} elements)";
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

            var codeGraph = projectData.CreateCodeStructure();


            // Load settings
            if (projectData.Settings.TryGetValue(nameof(IsInfoPanelVisible), out var isInfoPanelVisibleString))
            {
                IsInfoPanelVisible = bool.Parse(isInfoPanelVisibleString);
            }

            if (GraphViewModel != null &&
                projectData.Settings.TryGetValue(nameof(GraphViewModel.ShowFlatGraph), out var showFlatGraph))
            {
                GraphViewModel.ShowFlatGraph = bool.Parse(showFlatGraph);
            }

            if (projectData.Settings.TryGetValue(nameof(ProjectExclusionRegExCollection), out var projectExcludeRegEx))
            {
                _projectExclusionFilters.Initialize(projectExcludeRegEx, ";");
            }

            LoadCodeGraph(codeGraph);
            _isSaved = true;
        }
        catch (Exception)
        {
            MessageBox.Show("Failed loading project file", "Load error", MessageBoxButton.OK, MessageBoxImage.Error);
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
        projectData.AddCodeStructure(_codeGraph);
        projectData.Settings[nameof(IsInfoPanelVisible)] = IsInfoPanelVisible.ToString();
        projectData.Settings[nameof(GraphViewModel.ShowFlatGraph)] = _graphViewModel.ShowFlatGraph.ToString();
        projectData.Settings[nameof(ProjectExclusionRegExCollection)] = _projectExclusionFilters.ToString();

        // Add other settings here

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        var json = JsonSerializer.Serialize(projectData, options);
        File.WriteAllText(saveFileDialog.FileName, json);
        _isSaved = true;
    }

    private void CopyToExplorerGraph(CycleGroupViewModel vm)
    {
        var graph = vm.CycleGroup.CodeGraph;
        var codeElements = graph.Nodes.Values;
        var numberOfElements = codeElements.Count();

        if (numberOfElements > _warningCodeElementLimitForCycle)
        {
            if (MessageBoxResult.Yes !=
                MessageBox.Show(
                    $"There are {numberOfElements} code elements in this cycle. It may take a long time to render this data. Do you want to proceed?",
                    "Proceed?", MessageBoxButton.YesNo, MessageBoxImage.Warning))
            {
                return;
            }
        }

        var dependencies = new List<Dependency>();
        graph.DfsHierarchy(n => dependencies.AddRange(n.Dependencies));

        GraphViewModel?.Clear();
        GraphViewModel?.AddToGraph(codeElements, dependencies);

        SelectedTabIndex = 0;
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
            if (MessageBox.Show("Do you wan't to save the project so you don't have to import it again?", "Save",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                SaveProject();
            }
        }

        return true;
    }
}