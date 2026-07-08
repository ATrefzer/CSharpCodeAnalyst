using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using CSharpCodeAnalyst.AnalyzerSdk.Wpf;
using CSharpCodeAnalyst.Shared.Messages;
using CSharpCodeAnalyst.TreeMap;
using CSharpCodeAnalyst.TreeMap.Data;
using CSharpCodeAnalyst.TreeMap.Interfaces;

namespace CSharpCodeAnalyst.Features.History;

internal class HistoryViewModel : INotifyPropertyChanged
{
    private readonly MessageBus _messaging;

    public HistoryViewModel(MessageBus messaging)
    {
        _messaging = messaging;
        CollectCommand = new WpfCommand(OnCollect);

    
    }

    private void OnCollect()
    {
        var viewModel = new ImportHistoryDialogViewModel();
        var dialog = new ImportHistoryDialog(viewModel) { Owner = Application.Current.MainWindow };

        if (dialog.ShowDialog() == true)
        {
            var repoPath = viewModel.RepositoryPath;
            var outputFile = viewModel.OutputFilePath;
            // ...
        }
        
        var data = new HierarchicalData("Root");
        data.AddChild(new HierarchicalData("Child1", 100));
        data.AddChild(new HierarchicalData("Child1", 200));
        data.SumAreaMetrics();
        Data = new HierarchicalDataContext(data);
    }

    public ICommand CollectCommand { get; }

    public HierarchicalDataContext Data
    {
        get;
        set
        {
            if (Equals(value, field))
            {
                return;
            }

            field = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}