using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using CSharpCodeAnalyst.AnalyzerSdk.Notifications;
using CSharpCodeAnalyst.AnalyzerSdk.Wpf;
using CSharpCodeAnalyst.CodeParser.Parser;
using CSharpCodeAnalyst.History.Git;
using CSharpCodeAnalyst.History.Model;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.Messages;

namespace CSharpCodeAnalyst.Features.History;

public class HistoryProgressArgs : EventArgs
{
    public HistoryProgressArgs(string message)
    {
        Message = message;
    }

    public string Message { get; }
}

internal class HistoryViewModel : INotifyPropertyChanged
{
    private const string HotspotsTabId = "History.Hotspots";

    private readonly MessageBus _messaging;
    private readonly IUserNotification _ui;
    private HistoryDto _lastHistory;
    private string _lastOutputFilePath = string.Empty;
    private string _lastRepositoryPath = string.Empty;

    public HistoryViewModel(MessageBus messaging, IUserNotification ui)
    {
        _messaging = messaging;
        _ui = ui;
        CollectCommand = new WpfCommand(OnCollect);
    }

    public ICommand CollectCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<HistoryProgressArgs> OnProgress;

    private async void OnCollect()
    {
        try
        {
            var viewModel = new ImportHistoryDialogViewModel();

            viewModel.OutputFilePath = _lastOutputFilePath;
            viewModel.RepositoryPath = _lastRepositoryPath;

            var dialog = new ImportHistoryDialog(viewModel) { Owner = Application.Current.MainWindow };

            if (dialog.ShowDialog() == false)
            {
                return;
            }

            _lastRepositoryPath = viewModel.RepositoryPath;
            _lastOutputFilePath = viewModel.OutputFilePath;
            _lastHistory = null;


            // var data = new HierarchicalData("Root");
            // data.AddChild(new HierarchicalData("Child1", 100, 10));
            // data.AddChild(new HierarchicalData("Child1", 200, 100));
            // data.SumAreaMetrics();
            // data.NormalizeWeightMetrics();
            // data.RemoveLeafNodesWithoutArea();
            //
            // var context = new HierarchicalDataContext(data)
            // {
            //     AreaSemantic = "Area",
            //     WeightSemantic = "Weight"
            // };
            // _messaging.Publish(new ShowHierarchicalDataRequest(HotspotsTabId, Strings.History_Hotspots_TabTitle, context));

            var supported = LinesOfCodeFileTypes.GetFileTypes().Keys;
            var filter = new ExtensionIncludeFilter(supported.ToArray());



            OnProgress?.Invoke(this, new HistoryProgressArgs(Strings.History_Progress_Init));


            var progress = new Progress<string>(msg => OnProgress?.Invoke(this, new HistoryProgressArgs(msg)));

            HistoryDto dto = new HistoryDto();

            // Run in background so progress can pass.
            await Task.Run(() =>
            {
                var gitProvider = new GitProvider();
                gitProvider.Initialize(_lastRepositoryPath);
                dto.History = gitProvider.ExtractHistory(progress, true, filter);

                var metricProvider = new LinesOfCodeProvider(progress);
                dto.LinesOfCode = metricProvider.AnalyzeDirectory(_lastRepositoryPath);


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
            // Hide progress
            OnProgress?.Invoke(this, new HistoryProgressArgs(string.Empty));
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Collect all parts for persistence
    /// </summary>
    private class HistoryDto
    {
        public CSharpCodeAnalyst.History.Git.History? History { get; set; }

        public Dictionary<string, LinesOfCodeProvider.LinesOfCode>? LinesOfCode { get; set; }
    }
    
}