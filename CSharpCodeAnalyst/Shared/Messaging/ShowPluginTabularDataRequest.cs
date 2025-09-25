namespace CSharpCodeAnalyst.Shared.Messaging;

public class ShowPluginTabularDataRequest(Table.Table table)
{
    public Table.Table Table { get; } = table;
}