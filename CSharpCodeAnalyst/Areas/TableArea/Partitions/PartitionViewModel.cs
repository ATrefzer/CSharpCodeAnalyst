using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CSharpCodeAnalyst.CycleArea;
using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Areas.ResultArea;

public class PartitionViewModel : INotifyPropertyChanged
{
    private bool _isExpanded;

    public PartitionViewModel(string partitionName, IEnumerable<CodeElementLineViewModel> codeElements)
    {
        PartitionName = partitionName;
        CodeElements = new ObservableCollection<CodeElementLineViewModel>(codeElements);
        _isExpanded = false;
    }

    public string PartitionName { get; }
    public ObservableCollection<CodeElementLineViewModel> CodeElements { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (value == _isExpanded) return;
            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    public int ElementCount => CodeElements.Count;

    public string Description => string.Format(Strings.Partition_Description, ElementCount);

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}