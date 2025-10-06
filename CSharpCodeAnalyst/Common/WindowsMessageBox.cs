using System.Windows;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.UI;

namespace CSharpCodeAnalyst.Common;

internal class WindowsMessageBox : IMessageBox
{
    public void ShowError(string message)
    {
        MessageBox.Show(message, Strings.Error_Title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public void ShowSuccess(string message)
    {
        ToastManager.ShowSuccess(message, 2500);
    }
}