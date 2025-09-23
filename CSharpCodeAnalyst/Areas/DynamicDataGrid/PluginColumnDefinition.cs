using System.Windows.Input;

namespace CSharpCodeAnalyst.Areas.TableArea;

public class PluginColumnDefinition : IPluginColumnDefinition
{
    public string PropertyName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ColumnType Type { get; set; } = ColumnType.Text;
    public double Width { get; set; } = 0; // 0 = Auto
    public ICommand? ClickCommand { get; set; }
    public object? CommandParameter { get; set; }
}