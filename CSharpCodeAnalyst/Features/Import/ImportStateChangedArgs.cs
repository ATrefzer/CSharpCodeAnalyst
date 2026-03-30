namespace CSharpCodeAnalyst.Import;

public class ImportStateChangedArgs(string progressMessage, bool isLoading) : EventArgs
{
    public string ProgressMessage { get; set; } = progressMessage;
    public bool IsLoading { get; set; } = isLoading;
}