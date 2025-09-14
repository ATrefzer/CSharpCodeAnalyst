using System.Collections.ObjectModel;
using CSharpCodeAnalyst.Resources;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpCodeAnalyst.Areas.ResultArea;

public class PartitionsViewModel : TableViewModel
{
    private ObservableCollection<PartitionViewModel> _partitions = new();

    public PartitionsViewModel()
    {
        Title = Strings.Tab_Summary_Partitions;
    }

    public ObservableCollection<PartitionViewModel> Partitions
    {
        get => _partitions;
        set
        {
            if (Equals(value, _partitions)) return;
            _partitions = value;
            OnPropertyChanged();
        }
    }

    public override void Clear()
    {
        Partitions.Clear();
    }

}