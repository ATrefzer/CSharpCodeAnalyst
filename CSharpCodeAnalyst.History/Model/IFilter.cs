namespace CSharpCodeAnalyst.History.Model
{
    public interface IFilter
    {
        bool IsAccepted(string path);
    }
}