using System.Windows;

namespace CSharpCodeAnalyst.Areas.TableArea;

public interface IPluginDataProvider
{
    IEnumerable<object> GetData();
}