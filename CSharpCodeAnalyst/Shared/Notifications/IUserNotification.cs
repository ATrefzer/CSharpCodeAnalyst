namespace CSharpCodeAnalyst.Common;

public interface IUserNotification
{
    void ShowError(string message);

    void ShowSuccess(string message);

    void ShowInfo(string message);

    void ShowWarning(string message);
    string? ShowOpenFileDialog(string filter, string title);
    string? ShowSaveFileDialog(string filter, string title);
    void ShowErrorWarningDialog(List<string> errors, List<string> warnings);
    bool AskYesNoQuestion(string saveMessage, string saveTitle);
}