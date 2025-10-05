using System.Diagnostics;

namespace CSharpCodeAnalyst.Common;

internal class ConsoleMessageBox : IMessageBox
{
    public void ShowError(string message)
    {
        Trace.TraceError(message);
    }

    public void ShowSuccess(string message)
    {
        Trace.TraceInformation(message);
    }
}