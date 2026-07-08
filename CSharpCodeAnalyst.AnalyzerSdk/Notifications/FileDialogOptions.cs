using System.Windows;

namespace CSharpCodeAnalyst.AnalyzerSdk.Notifications;

/// <summary>
///     Optional extras for <see cref="IUserNotification.ShowOpenFileDialog" /> /
///     <see cref="IUserNotification.ShowSaveFileDialog" /> beyond filter/title. All members are
///     optional; omitted ones fall back to the platform dialog's own defaults.
/// </summary>
public sealed record FileDialogOptions
{
    /// <summary>Default extension applied when the user doesn't type one.</summary>
    public string? DefaultExt { get; init; }

    /// <summary>Starting directory. Ignored if it doesn't exist.</summary>
    public string? InitialDirectory { get; init; }

    /// <summary>Pre-filled file name.</summary>
    public string? FileName { get; init; }

    /// <summary>Whether a save dialog asks before overwriting an existing file. Defaults to true.</summary>
    public bool OverwritePrompt { get; init; } = true;

    /// <summary>
    ///     Owner window for the dialog. Defaults to the application's main window; pass the enclosing
    ///     window explicitly when the dialog is opened from another modal dialog (nested modality).
    /// </summary>
    public Window? Owner { get; init; }
}
