namespace CSharpCodeAnalyst.Common;

public interface IMessageBox
{
    void ShowError(string message);

    void ShowSuccess(string message);
}