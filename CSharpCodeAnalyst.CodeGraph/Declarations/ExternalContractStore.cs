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
///         Filled from the parallel phase 2 of the parser, hence the concurrent dictionary.
///     </para>
/// </summary>
public sealed class ExternalContractStore
{
    private readonly ConcurrentDictionary<string, string> _contracts = new();

    public IReadOnlyDictionary<string, string> Contracts => _contracts;

    public int Count => _contracts.Count;

    public bool IsEmpty => _contracts.IsEmpty;

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

    public void Clear()
    {
        _contracts.Clear();
    }

    /// <summary>
    ///     Replaces the current contents. Used to refill the shared store after an import or when loading
    ///     a project.
    /// </summary>
    public void LoadFrom(IReadOnlyDictionary<string, string> contracts)
    {
        _contracts.Clear();
        foreach (var (id, contract) in contracts)
        {
            _contracts[id] = contract;
        }
    }
}
