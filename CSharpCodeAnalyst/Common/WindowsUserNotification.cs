using System.Windows;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.UI;

namespace CSharpCodeAnalyst.Common;

internal class WindowsUserNotification : IUserNotification
{
    public void ShowError(string message)
    {
        MessageBox.Show(message, Strings.Error_Title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public void ShowSuccess(string message)
    {
        ToastManager.ShowSuccess(message, 2500);
    }

    public void ShowInfo(string message)
    {
        MessageBox.Show(message, Strings.Info,
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowWarning(string message)
    {
        MessageBox.Show(message, Strings.Warning_Title,
            MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}