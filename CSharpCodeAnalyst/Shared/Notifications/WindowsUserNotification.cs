using System.IO;
using System.Windows;
using CSharpCodeAnalyst.AnalyzerSdk.Notifications;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.UI;
using Microsoft.Win32;

namespace CSharpCodeAnalyst.Shared.Notifications;

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
    
    public string? ShowOpenFileDialog(string filter, string title, FileDialogOptions? options = null)
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            Title = title
        };
        ApplyCommonOptions(dialog, options);

        return ShowDialog(dialog, options?.Owner) == true ? dialog.FileName : null;
    }

    public string? ShowSaveFileDialog(string filter, string title, FileDialogOptions? options = null)
    {
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            Title = title,
            OverwritePrompt = options?.OverwritePrompt ?? true
        };
        ApplyCommonOptions(dialog, options);

        return ShowDialog(dialog, options?.Owner) == true ? dialog.FileName : null;
    }

    public string? ShowFolderBrowserDialog(string title, string? initialDirectory = null, Window? owner = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title
        };

        if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return ShowDialog(dialog, owner) == true ? dialog.FolderName : null;
    }

    private static void ApplyCommonOptions(FileDialog dialog, FileDialogOptions? options)
    {
        if (options is null)
        {
            return;
        }

        if (options.DefaultExt is not null)
        {
            dialog.DefaultExt = options.DefaultExt;
        }

        if (options.FileName is not null)
        {
            dialog.FileName = options.FileName;
        }

        if (!string.IsNullOrEmpty(options.InitialDirectory) && Directory.Exists(options.InitialDirectory))
        {
            dialog.InitialDirectory = options.InitialDirectory;
        }
    }

    /// <summary>
    ///     Shows a common dialog owned by <paramref name="owner" />, falling back to the application's
    ///     main window so every dialog is consistently parented instead of floating unowned.
    /// </summary>
    private static bool? ShowDialog(CommonDialog dialog, Window? owner)
    {
        var effectiveOwner = owner ?? Application.Current?.MainWindow;
        return effectiveOwner is not null ? dialog.ShowDialog(effectiveOwner) : dialog.ShowDialog();
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