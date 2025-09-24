using CSharpCodeAnalyst.PluginContracts;

namespace CSharpCodeAnalyst.Messages;

public class ShowPluginResult(Table table)
{
    public Table Table { get; } = table;
}