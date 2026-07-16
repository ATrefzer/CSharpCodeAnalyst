// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;

namespace DsmSuite.DsmViewer.View.Editing
{
    /// <summary>
    /// Interaction logic for ElementEditDialog.xaml
    /// </summary>
    public partial class ElementEditDialog
    {
        public ElementEditDialog()
        {
            InitializeComponent();
        }

        private void OnOkButtonClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
