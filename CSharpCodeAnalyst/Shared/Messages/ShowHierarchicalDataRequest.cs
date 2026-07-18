using CSharpCodeAnalyst.AnalyzerSdk.Messages;
using CSharpCodeAnalyst.TreeMap;

namespace CSharpCodeAnalyst.Shared.Messages;


/// <summary>
///     Requests a dynamic tree-map tab. <see cref="Id" /> keys the tab: publishing again under the
///     same id updates the existing tab instead of creating a duplicate. Different ids get their own,
///     parallel tabs.
/// </summary>
public class ShowHierarchicalDataRequest(string id, string title, HierarchicalDataContext data)
{
    public string Id { get; } = id;
    public string Title { get; } = title;
    public HierarchicalDataContext Data { get; } = data;
    
    public RequestOpenMode OpenMode { get; init; } = RequestOpenMode.Normal;
}

