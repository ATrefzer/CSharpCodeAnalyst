using System.Windows;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.UI;
using Microsoft.Win32;

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
    
    public string? ShowOpenFileDialog(string filter, string title)
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            Title = title
        };
        
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
    
    public string? ShowSaveFileDialog(string filter, string title)
    {
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            Title = title
        };
        
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
    
    public void ShowErrorWarningDialog(List<string> errors, List<string> warnings)
    {
        ErrorWarningDialog.Show(errors, warnings, Application.Current.MainWindow);
    }

    public bool AskYesNoQuestion(string message, string title)
    {
        return MessageBox.Show(message, title,
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }
}