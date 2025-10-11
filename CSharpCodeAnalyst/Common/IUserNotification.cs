namespace CSharpCodeAnalyst.Common;

public interface IUserNotification
{
    void ShowError(string message);

    void ShowSuccess(string message);

    void ShowInfo(string message);

    void ShowWarning(string message);
}