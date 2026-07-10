using System.Windows;
using CSharpCodeAnalyst.Contracts;
using CSharpCodeAnalyst.TreeMap.TreeMap;

namespace CSharpCodeAnalyst.TreeMap.Common
{
    internal sealed class HitTest
    {
        /// <summary>
        /// The layout must have been calculated - pass the map the renderer produced.
        /// </summary>
        public IHierarchicalData? Hit(IHierarchicalData item, Point mousePos, TreeMapLayout layout)
        {
            var rect = layout.Get(item);
            if (rect == null)
            {
                return null;
            }

            // We may find a more detailed hit deeper.
            IHierarchicalData? best = null;
            if (rect.IsHit(mousePos))
            {
                best = item;
                if (item.IsLeafNode)
                {
                    return item;
                }
            }

            foreach (var child in item.Children)
            {
                if (layout.Get(child)?.IsHit(mousePos) == true)
                {
                    return Hit(child, mousePos, layout);
                }
            }

            return best;
        }
    }
}
