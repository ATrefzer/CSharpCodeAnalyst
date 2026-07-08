using System.Windows;
using CSharpCodeAnalyst.TreeMap.Interfaces;

namespace CSharpCodeAnalyst.TreeMap.Common
{
    internal sealed class HitTest
    {
        /// <summary>
        /// Layout must have been called
        /// </summary>
        public IHierarchicalData? Hit(IHierarchicalData item, Point mousePos)
        {
            // We may find a more detailed hit deeper.
            IHierarchicalData? best = null;
            if (item.Layout == null)
            {
                return null;
            }

            if (item.Layout.IsHit(mousePos))
            {
                best = item;
                if (item.IsLeafNode)
                {
                    return item;
                }
            }

            foreach (var child in item.Children)
            {
                if (child.Layout.IsHit(mousePos))
                {
                    return Hit(child, mousePos);
                }
            }

            return best;
        }
    }
}
