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

    public string? ShowOpenFileDialog(string filter, string title)
    {
        // Not in console mode
        return null;
    }

    public string? ShowSaveFileDialog(string filter, string title)
    {
        // Not in console mode
        return null;
    }

    public void ShowErrorWarningDialog(List<string> errors, List<string> warnings)
    {
    }

    public bool AskYesNoQuestion(string saveMessage, string saveTitle)
    {
        return true;
    }
}