using System.Collections;
using System.Windows.Controls;
using CSharpCodeAnalyst.TreeMap.Interfaces;

namespace CSharpCodeAnalyst.TreeMap
{
    public sealed class DataGridViewUserCommands<T> : IDataGridViewUserCommands
    {
        private readonly Dictionary<MenuItem, Action<List<T>>> _menuItemToAction = new Dictionary<MenuItem, Action<List<T>>>();

        public bool Empty => !_menuItemToAction.Any();

        public bool Fill(ContextMenu contextMenu, IEnumerable selectedItems)
        {
            if (Empty)
            {
                return false;
            }

            var selection = selectedItems.OfType<T>().ToList();
            foreach (var item in _menuItemToAction)
            {
                var menuItem = item.Key;
                // Detach context menu items from previous shown context menu (if any)
                var parent = menuItem.Parent as ContextMenu;
                parent?.Items.Clear();
                menuItem.IsEnabled = selection.Any();
                menuItem.Command = new DelegateCommand(() => OnMenuClick(menuItem, selection));
                contextMenu.Items.Add(item.Key);
            }

            return true;
        }

        // Note: Action<List<object>> is accepted
        public void Register(string label, Action<List<T>> action)
        {
            var menuItem = new MenuItem();
            menuItem.Header = label;
            _menuItemToAction.Add(menuItem, action);
        }

        private void OnMenuClick(MenuItem item, List<T> selectedItems)
        {
            var action = _menuItemToAction[item];

            // To list of object works, so the action can be of type Action<List<object>>
            // This is because the HierarchicalDataViewBase calls OfType<T>
            // So any base type is accepted.
            action.Invoke(selectedItems);
        }
    }
}