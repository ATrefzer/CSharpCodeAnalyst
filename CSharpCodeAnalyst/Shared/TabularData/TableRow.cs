using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CSharpCodeAnalyst.Shared.Table;

public abstract class TableRow : INotifyPropertyChanged
{
    private bool _isExpanded;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (value == _isExpanded)
            {
                return;
            }

            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}