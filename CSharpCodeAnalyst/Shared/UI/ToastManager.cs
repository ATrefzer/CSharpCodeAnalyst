using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace CSharpCodeAnalyst.Shared.UI;

public static class ToastManager
{
    private static Panel? _toastContainer;

    public static void Initialize(Panel container)
    {
        _toastContainer = container;
    }

    public static void ShowSuccess(string message, int durationMs = 2000)
    {
        ShowToast(message, durationMs);
    }

    public static void ShowInfo(string message, int durationMs = 2000)
    {
        ShowToast(message, durationMs);
    }

    private static void ShowToast(string message, int durationMs)
    {
        if (_toastContainer == null)
        {
            // Fallback to MessageBox if not initialized
            MessageBox.Show(message, "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Debug.WriteLine($"Creating toast, container has {_toastContainer.Children.Count} children");

        var toast = new ToastNotification();

        //Canvas.SetLeft(toast, 100);
        Canvas.SetRight(toast, 20);
        Canvas.SetTop(toast, 100);
        Panel.SetZIndex(toast, 1000);

        _toastContainer.Children.Add(toast);

        // Show the toast
        toast.Show(message, durationMs);

        // Remove from container after animation completes
        var removeTimer = new DispatcherTimer();
        removeTimer.Interval = TimeSpan.FromMilliseconds(durationMs + 500);
        removeTimer.Tick += (s, e) =>
        {
            removeTimer.Stop();
            _toastContainer.Children.Remove(toast);
        };
        removeTimer.Start();
    }
}