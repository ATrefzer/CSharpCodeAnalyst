namespace CSharpCodeAnalyst.Common;

class ConsoleMessageBox : IMessageBox
{
    public void ShowError(string message)
    {
        Console.WriteLine(message);
    }
}