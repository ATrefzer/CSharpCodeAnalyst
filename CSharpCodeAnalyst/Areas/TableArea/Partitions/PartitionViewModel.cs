using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CSharpCodeAnalyst.CycleArea;
using CSharpCodeAnalyst.PluginContracts;
using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Areas.TableArea.Partitions;

public class PartitionViewModel : TableRow
{
    private string _partitionName;

    public PartitionViewModel(string partitionName, IEnumerable<CodeElementLineViewModel> codeElements)
    {
        PartitionName = partitionName;
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

 

    public int ElementCount => CodeElements.Count;

    public string Description => string.Format(Strings.Partition_Description, ElementCount);

}