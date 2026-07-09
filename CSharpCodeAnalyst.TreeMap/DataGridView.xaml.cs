using System.Collections;
using System.Windows;
using System.Windows.Controls;
using CSharpCodeAnalyst.TreeMap.Interfaces;

namespace CSharpCodeAnalyst.TreeMap
{
    /// <summary>
    /// Interaction logic for DataGridView.xaml
    /// </summary>
    public partial class DataGridView
    {
        public DataGridView()
        {
            InitializeComponent();
            DataContextChanged += DataGridView_DataContextChanged;
        }

        private void DataGridView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            _dataGrid.ItemsSource = DataContext as IEnumerable;
        }

        /// <summary>
        /// Commands that apply to a selection of the data grid.
        /// </summary>
        public IDataGridViewUserCommands UserCommands
        {
            get => (IDataGridViewUserCommands)GetValue(UserCommandsProperty);
            set => SetValue(UserCommandsProperty, value);
        }


        public static readonly DependencyProperty UserCommandsProperty = DependencyProperty.Register(
                                                                                                     "UserCommands", typeof(IDataGridViewUserCommands), typeof(DataGridView), new PropertyMetadata(null));


        private void _dataGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            _contextMenu.Items.Clear();

            if (UserCommands == null)
            {
                e.Handled = true;
                return;
            }

            // Build the context menu
            var filled = UserCommands.Fill(_contextMenu, _dataGrid.SelectedItems);
            e.Handled = !filled;
        }
    }
}
