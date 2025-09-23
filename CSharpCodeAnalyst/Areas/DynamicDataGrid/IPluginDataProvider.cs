using System.Windows;

namespace CSharpCodeAnalyst.Areas.TableArea;

public interface IPluginDataProvider
{
    IEnumerable<ITableColumnDefinition> GetColumns();
    IEnumerable<object> GetData();
    DataTemplate? GetCustomTemplate(); // Optional für komplexe Fälle
}