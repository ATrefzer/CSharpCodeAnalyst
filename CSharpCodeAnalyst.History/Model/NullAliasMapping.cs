namespace CSharpCodeAnalyst.History.Model;

/// <summary>No-op alias mapping: every name maps only to itself.</summary>
public sealed class NullAliasMapping : IAliasMapping
{
    public string GetAlias(string name)
    {
        return name;
    }

    public IEnumerable<string> GetReverse(string alias)
    {
        return [alias];
    }
}
