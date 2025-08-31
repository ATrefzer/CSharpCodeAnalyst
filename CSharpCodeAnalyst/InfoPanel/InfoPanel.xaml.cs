using System.Windows;
using System.Windows.Controls;

namespace CSharpCodeAnalyst.InfoPanel
{
    /// <summary>
    /// Interaction logic for InfoPanel.xaml
    /// </summary>
    public partial class InfoPanel : UserControl
    {
        public static readonly DependencyProperty IsVisibleProperty =
            DependencyProperty.Register("IsVisible", typeof(bool), typeof(InfoPanel), 
                new PropertyMetadata(true, OnIsVisibleChanged));

        public bool IsVisible
        {
            get => (bool)GetValue(IsVisibleProperty);
            set => SetValue(IsVisibleProperty, value);
        }

        public InfoPanel()
        {
            InitializeComponent();
        }

        private static void OnIsVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is InfoPanel panel)
            {
                panel.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}