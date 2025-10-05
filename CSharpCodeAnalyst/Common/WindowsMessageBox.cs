using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.UI;

namespace CSharpCodeAnalyst.Common;

internal class WindowsMessageBox : IMessageBox
{
    public void ShowError(string message)
    {
        System.Windows.MessageBox.Show(message, Strings.Error_Title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
    }

    public void ShowSuccess(string message)
    {
        ToastManager.ShowSuccess(message, 2500);
    }
}