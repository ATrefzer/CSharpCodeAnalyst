using System.Diagnostics;
using System.Windows;

namespace CSharpCodeAnalyst.Shared.UI;

public enum ToastType
{
    Success,
    Warning
}

public static class ToastManager
{
    private static readonly List<ToastNotification> _activeToasts = new();
    private static readonly double ToastSpacing = 10; // Spacing between stacked toasts
    private static readonly double RightMargin = 20;
    private static readonly double TopMargin = 100;

    public static void ShowSuccess(string message, int durationMs = 2000)
    {
        ShowToast(message, durationMs);
    }

    public static void ShowInfo(string message, int durationMs = 2000)
    {
        ShowToast(message, durationMs);
    }

    public static void ShowWarning(string message, int durationMs = 3000)
    {
        ShowToast(message, durationMs, ToastType.Warning);
    }

    private static void ShowToast(string message, int durationMs, ToastType type = ToastType.Success)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            var toast = new ToastNotification();

            // Calculate position relative to screen (top-right corner)
            var workingArea = SystemParameters.WorkArea;

            // Position at top-right of screen
            // We'll adjust after the window loads, and we know its actual size
            toast.Loaded += (s, e) =>
            {
                PositionToast(toast, workingArea);
            };

            // Track active toasts
            _activeToasts.Add(toast);

            // Remove from tracking when closed
            toast.Closed += (s, e) =>
            {
                _activeToasts.Remove(toast);
                RepositionToasts(workingArea);
            };

            // Show the toast
            toast.ShowToast(message, durationMs, type);
        });
    }

    private static void PositionToast(ToastNotification toast, Rect workingArea)
    {
        // Calculate vertical offset based on number of existing toasts
        double topOffset = TopMargin;

        int index = _activeToasts.IndexOf(toast);
        for (int i = 0; i < index; i++)
        {
            if (_activeToasts[i].IsLoaded)
            {
                topOffset += _activeToasts[i].ActualHeight + ToastSpacing;
            }
        }

        // Position at top-right corner
        toast.Left = workingArea.Right - toast.ActualWidth - RightMargin;
        toast.Top = workingArea.Top + topOffset;
    }

    private static void RepositionToasts(Rect workingArea)
    {
        // Reposition all active toasts to fill gaps
        double topOffset = TopMargin;

        foreach (var toast in _activeToasts)
        {
            if (toast.IsLoaded)
            {
                toast.Top = workingArea.Top + topOffset;
                topOffset += toast.ActualHeight + ToastSpacing;
            }
        }
    }
}