using CSharpCodeAnalyst.Shared.DynamicDataGrid.Contracts.TabularData;

namespace CSharpCodeAnalyst.Shared.Messages;

/// <summary>
///     Requests a dynamic result tab. <see cref="Id" /> keys the tab: publishing again under the
///     same id updates the existing tab instead of creating a duplicate (e.g. re-running the same
///     analyzer). Different ids get their own, parallel tabs.
/// </summary>
public class ShowTabularDataRequest(string id, string title, Table table)
{
    public string Id { get; } = id;
    public string Title { get; } = title;
    public Table Table { get; } = table;
}
