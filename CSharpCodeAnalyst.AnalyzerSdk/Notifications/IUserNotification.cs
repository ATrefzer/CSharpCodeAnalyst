using System.Windows;

namespace CSharpCodeAnalyst.AnalyzerSdk.Notifications;

public interface IUserNotification
{
    void ShowError(string message);

    void ShowSuccess(string message);

    void ShowInfo(string message);

    void ShowWarning(string message);
    string? ShowOpenFileDialog(string filter, string title, FileDialogOptions? options = null);
    string? ShowSaveFileDialog(string filter, string title, FileDialogOptions? options = null);
    string? ShowFolderBrowserDialog(string title, string? initialDirectory = null, Window? owner = null);
    void ShowErrorWarningDialog(List<string> errors, List<string> warnings);
    bool AskYesNoQuestion(string saveMessage, string saveTitle);
}
