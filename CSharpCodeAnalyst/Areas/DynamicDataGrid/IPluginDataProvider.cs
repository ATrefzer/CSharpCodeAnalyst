namespace CSharpCodeAnalyst.Areas.TableArea;

public interface IPluginDataProvider
{
    IEnumerable<object> GetData();
}