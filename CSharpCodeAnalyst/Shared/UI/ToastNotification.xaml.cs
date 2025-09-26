using System.Windows;
using System.Windows.Media.Animation;

namespace CSharpCodeAnalyst.Shared.UI;

public partial class ToastNotification
{
    public ToastNotification()
    {
        InitializeComponent();
    }

    public void Show(string message, int durationMs = 2000)
    {
        MessageText.Text = message;
        Visibility = Visibility.Visible;

        // Start with fade-in
        var fadeInAnimation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(600));
        BeginAnimation(OpacityProperty, fadeInAnimation);
    }
}