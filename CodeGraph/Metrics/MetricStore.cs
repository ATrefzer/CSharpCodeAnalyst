namespace CodeGraph.Metrics;

/// <summary>
///     Holds the optional per-member source metrics, keyed by <see cref="Graph.CodeElement.Id" />.
///     Kept beside the code graph (not on the elements) so the graph model stays pure and the store
///     is trivially optional: an import without metric collection simply leaves it empty.
/// </summary>
public sealed class MetricStore
{
    private readonly Dictionary<string, MemberMetrics> _metrics = new();

    public IReadOnlyDictionary<string, MemberMetrics> Metrics => _metrics;

    public int Count => _metrics.Count;

    public bool IsEmpty => _metrics.Count == 0;

    public void Add(string elementId, MemberMetrics metrics)
    {
        _metrics[elementId] = metrics;
    }

    public MemberMetrics? TryGet(string elementId)
    {
        return _metrics.GetValueOrDefault(elementId);
    }

    public void Clear()
    {
        _metrics.Clear();
    }

    /// <summary>
    ///     Replaces the current contents with the given metrics. Used to refill the shared store
    ///     after an import or when loading a project.
    /// </summary>
    public void LoadFrom(IReadOnlyDictionary<string, MemberMetrics> metrics)
    {
        _metrics.Clear();
        foreach (var (id, m) in metrics)
        {
            _metrics[id] = m;
        }
    }
}
