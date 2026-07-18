// SPDX-License-Identifier: GPL-3.0-or-later
using DsmSuite.DsmViewer.Model.Interfaces;
using DsmSuite.DsmViewer.ViewModel.Common;

namespace DsmSuite.DsmViewer.ViewModel.Lists
{
    public class ElementListItemViewModel : ViewModelBase, IComparable
    {
        private IDsmElement _element;

        public ElementListItemViewModel(IDsmElement element, int weight)
        {
            _element = element;
            ElementName = element.Name;
            ElementPath = element.Parent.Fullname;
            ElementType = element.Type;
            Properties = _element.Properties;
            Weight = weight;
        }

        public int Index { get; set; }
        public string ElementPath { get; }
        public string ElementName { get; }
        public string ElementType { get; }
        public int Weight { get; }
        public IDictionary<string, string> Properties { get; }

        public IEnumerable<string> DiscoveredElementPropertyNames()
        {
            return _element.DiscoveredElementPropertyNames();
        }

        public int CompareTo(object obj)
        {
            ElementListItemViewModel other = obj as ElementListItemViewModel;
            int res;

            res = string.Compare(ElementPath, other?.ElementPath, StringComparison.Ordinal);
            if (res == 0)
                res = string.Compare(ElementName, other?.ElementName, StringComparison.Ordinal);
            if (res == 0)
                res = Weight.CompareTo(other.Weight);
            if (res == 0)
                res = string.Compare(ElementType, other?.ElementType, StringComparison.Ordinal);
            return res;
        }
    }
}
