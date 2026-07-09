using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using CSharpCodeAnalyst.AnalyzerSdk.Messages;
using CSharpCodeAnalyst.AnalyzerSdk.Notifications;
using CSharpCodeAnalyst.AnalyzerSdk.Wpf;
using CSharpCodeAnalyst.History.Analyzer;
using CSharpCodeAnalyst.History.Git;
using CSharpCodeAnalyst.History.Metrics;
using CSharpCodeAnalyst.History.Model;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared;
using CSharpCodeAnalyst.Shared.Messages;
using CSharpCodeAnalyst.Shared.UI;
using CSharpCodeAnalyst.TreeMap;
using CSharpCodeAnalyst.TreeMap.Data;

namespace CSharpCodeAnalyst.Features.History;

internal class HistoryViewModel : INotifyPropertyChanged
{
    private const string HotspotsTabId = "History.Hotspots";

    private readonly IProgress<BusyState> _busy;
    private readonly MessageBus _messaging;
    private readonly IUserNotification _ui;
    private HistoryDto? _lastHistory;
    private string _lastOutputFilePath = string.Empty;
    private string _lastRepositoryPath = string.Empty;

    public HistoryViewModel(MessageBus messaging, IUserNotification ui, IProgress<BusyState> busy)
    {
        _messaging = messaging;
        _ui = ui;
        _busy = busy;
        CollectCommand = new WpfCommand(OnCollect);
        LoadCommand = new WpfCommand(OnLoad);
        HotspotsCommand = new WpfCommand(OnHotspots);
        ChangeCouplingCommand = new WpfCommand(OnChangeCoupling);
    }

    public ICommand CollectCommand { get; }

    public ICommand LoadCommand { get; }

    public ICommand HotspotsCommand { get; }

    public ICommand ChangeCouplingCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private async void OnCollect()
    {
        try
        {
            var viewModel = new ImportHistoryDialogViewModel();

            viewModel.OutputFilePath = _lastOutputFilePath;
            viewModel.RepositoryPath = _lastRepositoryPath;

            var dialog = new ImportHistoryDialog(viewModel, _ui) { Owner = Application.Current.MainWindow };

            if (dialog.ShowDialog() == false)
            {
                return;
            }

            _lastRepositoryPath = viewModel.RepositoryPath;
            _lastOutputFilePath = viewModel.OutputFilePath;
            _lastHistory = null;

            var supported = LinesOfCodeFileTypes.GetFileTypes().Keys;
            var filter = new ExtensionIncludeFilter(supported.ToArray());

            _busy.Report(new BusyState(Strings.History_Progress_Init, true));

            var progress = new Progress<string>(msg => _busy.Report(new BusyState(msg, true)));

            var dto = new HistoryDto();
            dto.SupportedFilesExtensions = supported.ToList();

            // Run in background so progress can pass.
            await Task.Run(() =>
            {
                var gitProvider = new GitProvider();
                gitProvider.Initialize(_lastRepositoryPath);

                // The filter is needed only for the contributions. The history contains all files.
                // It is the known file types we can also calculate lines of code for. Avoids calculating
                // contribution for binary files etc.
                dto.History = gitProvider.ExtractHistory(progress, true, filter);

                // Collect metrics only for tracked files.
                var trackedFiles = gitProvider.GetAllTrackedLocalFiles();
                var trackedFilesFilter = new FileFilter(trackedFiles);
                var metricProvider = new LinesOfCodeProvider(progress);
                dto.LinesOfCode = metricProvider.AnalyzeDirectory(_lastRepositoryPath, trackedFilesFilter);

                // Write file
                var options = new JsonSerializerOptions { WriteIndented = false };
                var json = JsonSerializer.Serialize(dto, options);
                File.WriteAllText(_lastOutputFilePath, json);
            });

            _lastHistory = dto;
            _ui.ShowSuccess(Strings.History_Extracted);
        }
        catch (Exception e)
        {
            _ui.ShowError(string.Format(Strings.OperationFailed_Message, e.Message));
            Trace.WriteLine($"Failed {nameof(OnCollect)} {e}");
        }
        finally
        {
            _busy.Report(new BusyState(string.Empty, false));
        }
    }

    private void OnLoad()
    {
        var path = _ui.ShowOpenFileDialog(Strings.History_JsonFilesFilter, Strings.History_LoadDialogTitle);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions { WriteIndented = false };
            _lastHistory = JsonSerializer.Deserialize<HistoryDto>(json, options);
            _lastOutputFilePath = path;
            _ui.ShowSuccess(Strings.History_Loaded);
        }
        catch (Exception e)
        {
            _ui.ShowError(string.Format(Strings.OperationFailed_Message, e.Message));
            Trace.WriteLine($"Failed {nameof(OnLoad)} {e}");
        }
    }

    private void OnHotspots()
    {
        if (_lastHistory is null || _lastHistory.History is null || _lastHistory.LinesOfCode is null)
        {
            ToastManager.ShowWarning(Strings.History_NoDataLoaded);
            return;
        }

        var analyzer = new CSharpCodeAnalyst.History.Analyzer.Analyzers();
        var result = analyzer.AnalyzeHotspots(_lastHistory.History.ChangeSets, _lastHistory.LinesOfCode);

        // Format to hierarchical (tree-map) data. The HotspotNode tree arrives cleaned
        // (no empty branches, no leaves without area - see HotspotBuilder.Build) and with
        // raw weights (commit counts); normalizing them for coloring is owned by the
        // tree-map view. Only the area sums must be computed here, because the conversion
        // does not carry them over.
        var root = ToHierarchicalData(result);
        root.SumAreaMetrics();

        var data = new HierarchicalDataContext(root)
        {
            AreaSemantic = Strings.Hotspots_AreaSemantic,
            WeightSemantic = Strings.Hotspots_WeightSemantic
        };

        _messaging.Publish(new ShowHierarchicalDataRequest("ID_Hotspots", Strings.Hotspots_Tab_Title, data));
    }

    /// <summary>
    ///     Converts the UI-free <see cref="HotspotNode" /> tree from the analyzer into the TreeMap
    ///     control's own <see cref="HierarchicalData" />. Areas and weights are carried over raw;
    ///     only <see cref="HierarchicalData.SumAreaMetrics" /> needs to run again on the result
    ///     (it also sorts children by descending area, which the renderer relies on).
    /// </summary>
    private static HierarchicalData ToHierarchicalData(HotspotNode node)
    {
        var data = node.IsLeafNode
            ? new HierarchicalData(node.Name, node.AreaMetric, node.WeightMetric)
            : new HierarchicalData(node.Name);

        data.Description = node.Description;
        data.ColorKey = node.ColorKey;
        data.Tag = node.Tag;

        foreach (var child in node.Children)
        {
            data.AddChild(ToHierarchicalData(child));
        }

        return data;
    }

    private void OnChangeCoupling()
    {
        if (_lastHistory is null || _lastHistory.History is null)
        {
            ToastManager.ShowWarning(Strings.History_NoDataLoaded);
            return;
        }

        var analyzer = new CSharpCodeAnalyst.History.Analyzer.Analyzers();
        var result = analyzer.AnalyzeChangeCoupling(_lastHistory.History.ChangeSets);

        if (result.Count == 0)
        {
            ToastManager.ShowWarning(Strings.History_NoCouplingsFound);
            return;
        }

        // Format to table data
        var table = new ChangeCouplingsViewModel(result, _messaging);
        _messaging.Publish(new ShowTabularDataRequest("ID_ChangeCouplings", Strings.ChangeCoupling_Tab_Title, table));
    }


    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    ///     Collect all parts for persistence
    /// </summary>
    private class HistoryDto
    {
        public CSharpCodeAnalyst.History.Git.History? History { get; set; }

        public Dictionary<string, LinesOfCodeProvider.LinesOfCode>? LinesOfCode { get; set; }
        public List<string>? SupportedFilesExtensions { get; set; }
    }
}