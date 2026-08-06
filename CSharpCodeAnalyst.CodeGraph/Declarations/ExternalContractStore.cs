using System.Collections.Concurrent;

namespace CSharpCodeAnalyst.CodeGraph.Declarations;

/// <summary>
///     Records which members implement or override a contract that is <b>not part of the analyzed
///     code</b> - a framework interface member (<c>ICommand.Execute</c>) or a base member from a
///     referenced assembly (<c>object.ToString</c>, <c>CSharpSyntaxVisitor.VisitGenericName</c>), keyed by
///     <see cref="Graph.CodeElement.Id" /> and valued with the contract's display name.
///     <para>
///         Such a member has no incoming reference anywhere in the graph - the caller is the framework -
///         so without this information it looks like dead code. The relationship model cannot carry the
///         fact: with external code excluded there is no element to point an <c>Overrides</c> edge at, and
///         with it included the edge is flattened to a <c>Uses</c> edge on the containing type, which is
///         indistinguishable from ordinary use of that type.
///     </para>
///     <para>
///         Kept beside the code graph rather than on <see cref="Graph.CodeElement" />, following
///         <see cref="Metrics.MetricStore" />: the graph model stays pure, the store is trivially optional
///         (an importer that knows nothing about this simply leaves it empty), and the shared
///         <see cref="Graph.CodeElement" /> type does not grow a field that only the C# parser ever fills.
///     </para>
///     <para>
///         Beside the member-level contracts the store carries one type-level fact: the
///         <see cref="NotifyingTypes" /> - every analyzed type that raises change notifications
///         (<c>INotifyPropertyChanged</c> anywhere in its interface set). The member-level route cannot
///         express this when the implementation sits in a base class outside the analyzed code
///         (<c>ObservableObject</c>, <c>BindableBase</c>, ...): the derived type then has no
///         <c>PropertyChanged</c> member of its own, so nothing in the graph says it is a view model.
///         The dead code analysis uses the set to keep public properties of such types - the ones a XAML
///         <c>{Binding}</c> may read - out of the highest confidence.
///     </para>
///     <para>
///         Filled from the parallel phase 2 of the parser, hence the concurrent dictionaries.
///     </para>
/// </summary>
public sealed class ExternalContractStore
{
    private readonly ConcurrentDictionary<string, string> _contracts = new();

    /// <summary>Value-less; a concurrent set does not exist in the BCL.</summary>
    private readonly ConcurrentDictionary<string, byte> _notifyingTypes = new();

    public IReadOnlyDictionary<string, string> Contracts => _contracts;

    /// <summary>The ids of the types that raise change notifications - see the class remarks.</summary>
    public IReadOnlyCollection<string> NotifyingTypes => _notifyingTypes.Keys.ToList();

    public int Count => _contracts.Count;

    public bool IsEmpty => _contracts.IsEmpty && _notifyingTypes.IsEmpty;

    /// <summary>
    ///     Records the contract for an element. A member can implement several external contracts; the
    ///     first one wins, because the store answers "is this member bound by code we cannot see" and one
    ///     example is enough to explain it.
    /// </summary>
    public void Add(string elementId, string contractName)
    {
        _contracts.TryAdd(elementId, contractName);
    }

    public string? TryGet(string elementId)
    {
        return _contracts.GetValueOrDefault(elementId);
    }

    public bool Contains(string elementId)
    {
        return _contracts.ContainsKey(elementId);
    }

    /// <summary>Records a type that raises change notifications.</summary>
    public void AddNotifyingType(string typeId)
    {
        _notifyingTypes.TryAdd(typeId, 0);
    }

    public void Clear()
    {
        _contracts.Clear();
        _notifyingTypes.Clear();
    }

    /// <summary>
    ///     Replaces the current contents. Used to refill the shared store after an import or when loading
    ///     a project.
    /// </summary>
    public void LoadFrom(IReadOnlyDictionary<string, string> contracts, IEnumerable<string> notifyingTypes)
    {
        Clear();
        foreach (var (id, contract) in contracts)
        {
            _contracts[id] = contract;
        }

        foreach (var typeId in notifyingTypes)
        {
            _notifyingTypes.TryAdd(typeId, 0);
        }
    }
}
