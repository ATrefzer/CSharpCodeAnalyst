namespace CSharpCodeAnalyst.History.Contracts
{
    public interface IFilter
    {
        bool IsAccepted(string path);
    }
}