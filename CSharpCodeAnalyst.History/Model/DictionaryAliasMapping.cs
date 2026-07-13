namespace CSharpCodeAnalyst.History.Model;

/// <summary>
///     In-memory alias mapping backed by a dictionary that is persisted together with the
///     history (see the HistoryDto), not a separate alias file. A name without an entry maps
///     to itself. Use this to group developers, for example onto teams, so the knowledge
///     analyses can be run per team instead of per developer.
/// </summary>
public sealed class DictionaryAliasMapping : IAliasMapping
{
    private readonly Dictionary<string, string> _aliasMapping;

    public DictionaryAliasMapping(IReadOnlyDictionary<string, string> aliasMapping)
    {
        _aliasMapping = new Dictionary<string, string>(aliasMapping);
    }

    public string GetAlias(string name)
    {
        return _aliasMapping.TryGetValue(name, out var alias) ? alias : name;
    }

    public IEnumerable<string> GetReverse(string alias)
    {
        return _aliasMapping.Where(m => m.Value == alias).Select(m => m.Key).ToList();
    }
}
