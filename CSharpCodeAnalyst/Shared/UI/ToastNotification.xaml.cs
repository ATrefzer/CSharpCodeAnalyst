using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace CSharpCodeAnalyst.Shared.UI;

public partial class ToastNotification
{
    private DispatcherTimer? _closeTimer;

    public ToastNotification()
    {
        InitializeComponent();
    }

    public void ShowToast(string message, int durationMs = 2000, ToastType type = ToastType.Success)
    {
        MessageText.Text = message;

        // Set colors based on type
        if (type == ToastType.Warning)
        {
            ToastBorder.Background = new SolidColorBrush(Color.FromRgb(255, 152, 0)); // Orange
            ToastBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(245, 124, 0));
        }
        else
        {
            ToastBorder.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
            ToastBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(69, 160, 73));
        }

        // Show the window
        Show();

        // Start with fade-in
        var fadeInAnimation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(600));
        BeginAnimation(OpacityProperty, fadeInAnimation);

        // Auto-close after duration
        _closeTimer = new DispatcherTimer();
        _closeTimer.Interval = TimeSpan.FromMilliseconds(durationMs);
        _closeTimer.Tick += (s, e) =>
        {
            _closeTimer.Stop();

            // Fade out before closing
            var fadeOutAnimation = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            fadeOutAnimation.Completed += (_, _) => Close();
            BeginAnimation(OpacityProperty, fadeOutAnimation);
        };
        _closeTimer.Start();
    }
}