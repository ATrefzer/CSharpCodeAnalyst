using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CSharpCodeAnalyst.Areas.ResultArea;

public abstract class TableViewModel : INotifyPropertyChanged
{
    public string Title { get; protected set; } = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;
    public abstract void Clear();

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}