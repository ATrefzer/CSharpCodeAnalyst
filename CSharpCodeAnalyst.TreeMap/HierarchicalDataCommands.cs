using System.Windows.Controls;
using CSharpCodeAnalyst.Contracts;

namespace CSharpCodeAnalyst.TreeMap
{
    public sealed class HierarchicalDataCommands
    {
        private readonly Dictionary<MenuItem, Action<IHierarchicalData>> _menuItemToAction = new Dictionary<MenuItem, Action<IHierarchicalData>>();

        public bool Fill(ContextMenu menu, IHierarchicalData data)
        {
            if (!_menuItemToAction.Any())
            {
                return false;
            }

            foreach (var pair in _menuItemToAction)
            {
                var menuItem = pair.Key;

                // Detach context menu items from previous shown context menu (if any)
                var parent = menuItem.Parent as ContextMenu;
                parent?.Items.Clear();
                menuItem.IsEnabled = data is { IsLeafNode: true };
                menuItem.Command = new DelegateCommand(() => OnMenuClick(menuItem, data));
                menu.Items.Add(menuItem);
            }

            return true;
        }

        public void Register(string title, Action<IHierarchicalData> action)
        {
            var item = new MenuItem { Header = title };

            _menuItemToAction[item] = action;
        }

        private void OnMenuClick(MenuItem item, IHierarchicalData data)
        {
            // Invoke subscriber with clicked data item.
            _menuItemToAction[item].Invoke(data);
        }
    }
}