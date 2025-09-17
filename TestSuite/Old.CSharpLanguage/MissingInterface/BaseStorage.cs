namespace CSharpLanguage.MissingInterface;

internal class BaseStorage
{
    // TODO Not detected as interface implementation
    public void Load()
    {
        Console.WriteLine("Load");
    }
}