using System.Collections.ObjectModel;
using CSharpCodeAnalyst.Areas.TableArea.CycleGroups;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.Table;

namespace CSharpCodeAnalyst.Areas.TableArea.Partitions;

public class PartitionViewModel : TableRow
{
    private string _partitionName;

    public PartitionViewModel(string partitionName, IEnumerable<CodeElementLineViewModel> codeElements)
    {
        _partitionName = partitionName;
        CodeElements = new ObservableCollection<CodeElementLineViewModel>(codeElements);
        //Title = partitionName;
    }

    public string PartitionName
    {
        get => _partitionName;
        set
        {
            if (value == _partitionName) return;
            _partitionName = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<CodeElementLineViewModel> CodeElements { get; }

    public int ElementCount
    {
        get => CodeElements.Count;
    }

    public string Description
    {
        get => string.Format(Strings.Partition_Description, ElementCount);
    }
}