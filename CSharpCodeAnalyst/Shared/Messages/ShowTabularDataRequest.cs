using CSharpCodeAnalyst.Shared.DynamicDataGrid.Contracts.TabularData;

namespace CSharpCodeAnalyst.Shared.Messages;

public class ShowTabularDataRequest(Table table)
{
    public Table Table { get; } = table;
}