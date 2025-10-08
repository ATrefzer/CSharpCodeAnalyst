using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CSharpCodeAnalyst.Shared.UI;

public partial class ToastNotification
{
    public ToastNotification()
    {
        InitializeComponent();
    }

    public void Show(string message, int durationMs = 2000, ToastType type = ToastType.Success)
    {
        MessageText.Text = message;
        Visibility = Visibility.Visible;

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

        // Start with fade-in
        var fadeInAnimation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(600));
        BeginAnimation(OpacityProperty, fadeInAnimation);
    }
}