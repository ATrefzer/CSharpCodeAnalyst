namespace CSharpCodeAnalyst.History.Model
{
    public interface IAliasMapping
    {
        string GetAlias(string name);
        IEnumerable<string> GetReverse(string alias);
    }
}