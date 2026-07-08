namespace CSharpCodeAnalyst.History.Contracts
{
    public interface IAliasMapping
    {
        string GetAlias(string name);
        IEnumerable<string> GetReverse(string alias);
    }
}