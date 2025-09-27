using CSharpCodeAnalyst.Shared.TabularData;

namespace CSharpCodeAnalyst.Shared.Messages;

public class ShowTabularDataRequest(Table table)
{
    public Table Table { get; } = table;
}