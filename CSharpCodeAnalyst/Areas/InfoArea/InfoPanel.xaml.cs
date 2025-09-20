using System.Windows;
using System.Windows.Controls;

namespace CSharpCodeAnalyst.InfoPanel
{
    /// <summary>
    /// Interaction logic for InfoPanel.xaml
    /// </summary>
    public partial class InfoPanel
    {
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