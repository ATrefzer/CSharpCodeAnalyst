using CSharpCodeAnalyst.PluginContracts;

namespace CSharpCodeAnalyst.Common;

public class ShowPluginResult(Table table)
{
    public Table Table { get; } = table;

}