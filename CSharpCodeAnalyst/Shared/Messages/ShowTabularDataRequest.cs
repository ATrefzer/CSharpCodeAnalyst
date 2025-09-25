namespace CSharpCodeAnalyst.Shared.Messaging;

public class ShowTabularDataRequest(Table.Table table)
{
    public Table.Table Table { get; } = table;
}