// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;

namespace DsmSuite.DsmViewer.View.Settings
{
    /// <summary>
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        private void OnOkButtonClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
