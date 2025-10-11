using System.Diagnostics;

namespace CSharpCodeAnalyst.Common;

internal class ConsoleUserNotification : IUserNotification
{
    public void ShowError(string message)
    {
        Trace.TraceError(message);
    }

    public void ShowSuccess(string message)
    {
        Trace.TraceInformation(message);
    }

    public void ShowInfo(string message)
    {
        Trace.TraceInformation(message);
    }

    public void ShowWarning(string message)
    {
        Trace.TraceWarning(message);
    }
}