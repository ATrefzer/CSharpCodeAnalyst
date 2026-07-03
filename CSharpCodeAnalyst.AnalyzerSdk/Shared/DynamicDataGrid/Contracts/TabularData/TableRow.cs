using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CSharpCodeAnalyst.Shared.DynamicDataGrid.Contracts.TabularData;

public abstract class TableRow : INotifyPropertyChanged
{

    public bool IsExpanded
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
}