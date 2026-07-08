using System.Diagnostics;
using System.Globalization;
using System.Windows;
using CSharpCodeAnalyst.TreeMap.Interfaces;

namespace CSharpCodeAnalyst.TreeMap.TreeMap
{
    /*
    Algorithm: https://www.win.tue.nl/~vanwijk/stm.pdf
    
    1. Start with a horizontal (height > width) or vertical (width > height) strip.
    2. We try adding items to the strip until the aspect ratio of 
       the rectangles in the strip gets worse than before.
    3. Once the strip is finished we repeat the steps with the remaining space and 
       remaining items.
    4. If all space is assigned to the items recursively proceed with the space 
       assigned to each item.

    Note that selecting horizontal or vertical splitting is decided each time we start a new strip.
    
    On a vertical strip, we stack items from the bottom to the top
    On a horizontal strip we stack items at the bottom from the left to the right
    
    We first organize the top level elements and then recursively render the children.
    This also allows to organize a border => outer boxes appear larger!
    
    Ensure that no items with 0 length area metric are in the data!
    Call SumAreaMetrics!
    */
    public sealed class SquarifiedTreeMapLayout
    {
        private int _level = -1;

        public bool DebugEnabled { get; set; }

        public void Layout(IHierarchicalData root, double width, double height)
        {
            var rect = new Rect(new Point(0, 0), new Size(width, height));
            var layout = GetLayout(root);
            layout.Rect = rect;
            Squarify(rect, root);
        }

        private static RectangularLayoutInfo GetLayout(IHierarchicalData data)
        {
            if (data.Layout == null)
            {
                data.Layout = new RectangularLayoutInfo();
            }

            return data.Layout as RectangularLayoutInfo;
        }

        private void Backup(List<IHierarchicalData> stripMembers)
        {
            foreach (var item in stripMembers)
            {
                item.Layout.Backup();
            }
        }

        private string Format(Rect rect)
        {
            var x = Math.Round(rect.X).ToString(CultureInfo.InvariantCulture);
            var y = Math.Round(rect.Y).ToString(CultureInfo.InvariantCulture);
            var width = Math.Round(rect.Width).ToString(CultureInfo.InvariantCulture);
            var height = Math.Round(rect.Height).ToString(CultureInfo.InvariantCulture);
            return $"({x},{y} -> ({width},{height})";
        }


        private string GetLevelIndent()
        {
            return new string(Enumerable.Repeat('\t', _level).ToArray());
        }

        /// <summary>
        /// Returns the updated available space left when the strip is subtracted.
        /// </summary>
        private Rect LayoutHorizontalStrip(Rect availableSpace, List<IHierarchicalData> stripMembers, double stripProportion)
        {
            var remainder = availableSpace;

            // Arrange along the Y axis
            var availableArea = availableSpace.Width * availableSpace.Height;
            var stripArea = availableArea * stripProportion;
            var stripWidth = availableSpace.Width;

            // Same for all items in the horizontal strip
            var stripHeight = stripArea / stripWidth;

            // Area metric used in the current strip
            var stripAreaMetric = stripMembers.Sum(x => x.AreaMetricSum);

            foreach (var item in stripMembers)
            {
                // Left to right

                // Proportion of the current item area withing the current strip
                var itemAreaProportion = item.AreaMetricSum / stripAreaMetric;
                var itemArea = stripArea * itemAreaProportion;
                var itemWidth = itemArea / stripHeight;

                // Update temporary layout information

                var layout = GetLayout(item);
                layout.Rect.Width = itemWidth;
                layout.Rect.Height = stripHeight;
                layout.Rect.X = remainder.X;
                layout.Rect.Y = availableSpace.Y + (availableSpace.Height - stripHeight);

                remainder.X = remainder.X + itemWidth;
                remainder.Width = Math.Abs(remainder.Width - stripWidth);
            }

            var newAvailableSpace = availableSpace;
            var height = Math.Abs(availableSpace.Height - stripHeight);
            newAvailableSpace.Height = height;
            return newAvailableSpace;
        }

        /// <summary>
        /// Returns the updated available space left when the strip is subtracted.
        /// </summary>
        private Rect LayoutStrip(Rect availableSpace, List<IHierarchicalData> stripMembers, double stripProportion, SplitDirection direction)
        {
            Debug.Assert(!double.IsNaN(stripProportion));
            if (direction == SplitDirection.Vertically)
            {
                return LayoutVerticalStrip(availableSpace, stripMembers, stripProportion);
            }

            return LayoutHorizontalStrip(availableSpace, stripMembers, stripProportion);
        }

        /// <summary>
        /// Returns the updated available space left when the strip is subtracted.
        /// availableSpace is all remaining space where the strip can be rendered.
        /// stripProportion Proportion of current strip within the remaining space (same as unassigned items).
        /// </summary>
        private Rect LayoutVerticalStrip(Rect availableSpace, List<IHierarchicalData> stripMembers, double stripProportion)
        {
            var remainingHeight = availableSpace.Height;

            // Arrange along the Y axis
            var availableArea = availableSpace.Width * availableSpace.Height;
            var stripArea = availableArea * stripProportion;

            // Same for all items in the vertical strip
            var stripHeight = availableSpace.Height;
            var stripWidth = stripArea / stripHeight;

            // Area metric used in the current strip
            var stripAreaMetric = stripMembers.Sum(x => x.AreaMetricSum);

            foreach (var item in stripMembers)
            {
                // Bottom to top

                // Proportion of the current item area withing the current strip
                var itemAreaProportion = item.AreaMetricSum / stripAreaMetric;
                var itemArea = stripArea * itemAreaProportion;
                var itemHeight = itemArea / stripWidth;

                // Works also!
                //var itemHeight = stripHeight * itemAreaProportion;

                // Update layout render information
                item.Layout = item.Layout ?? new RectangularLayoutInfo();
                var layout = (RectangularLayoutInfo)item.Layout;
                layout.Rect.Width = stripWidth;
                layout.Rect.Height = itemHeight;
                layout.Rect.X = availableSpace.X;
                layout.Rect.Y = availableSpace.Y + (remainingHeight - itemHeight);

                remainingHeight = Math.Abs(remainingHeight - itemHeight);
            }

            var newAvailableSpace = availableSpace;
            newAvailableSpace.X = availableSpace.X + stripWidth;
            newAvailableSpace.Width = Math.Abs(availableSpace.Width - stripWidth);

            return newAvailableSpace;
        }


        private void Rollback(List<IHierarchicalData> stripMembers)
        {
            foreach (var item in stripMembers)
            {
                item.Layout.Rollback();
            }
        }

        private void Squarify(Rect availableSpace, List<IHierarchicalData> itemsOrg)
        {
            // Copy of the list. All items to render in the available space.
            var items = new List<IHierarchicalData>(itemsOrg);
            var placed = new List<IHierarchicalData>();
            var remainingSpace = availableSpace;

            while (items.Any())
            {
                // Start a new strip to fill
                var direction = SplitDirection.Horizontally;
                if (remainingSpace.Width > remainingSpace.Height)
                {
                    direction = SplitDirection.Vertically;
                }

                // Area metrics of the remaining items not yet placed. This corresponds to the available space.
                // Once a strip is completed and the items have their place this needs to be recalculated.
                var areaMetricSum = items.Sum(x => x.AreaMetricSum);

                //if (areaMetricSum <= 0)
                //    return; Does this break the algo?
                Debug.Assert(areaMetricSum > 0);

                // Remaining space after the strip is finished (aspect ration gets worse or there are no more items)

                var currentStrip = new List<IHierarchicalData>(items.Count);
                var proceed = true;
                while (proceed && items.Any())
                {
                    // Try adding items until the aspect ration in the strip gets worse
                    var worst = WorstRatio(currentStrip);

                    var item = items.First();
                    currentStrip.Add(item);

                    // TODO TryAddToStrip

                    // Proportion of current strip within the remaining space.
                    var stripProportion = currentStrip.Sum(x => x.AreaMetricSum) / areaMetricSum;
                    var tmp = LayoutStrip(availableSpace, currentStrip, stripProportion, direction);

                    var newWorst = WorstRatio(currentStrip);
                    if (newWorst <= worst)
                    {
                        // Successfully added to strip
                        items.RemoveAt(0);
                        Backup(currentStrip);
                        remainingSpace = tmp;
                    }
                    else
                    {
                        // Failed adding the item. It would make things worse. Rollback.
                        currentStrip.RemoveAt(currentStrip.Count - 1);
                        Rollback(currentStrip);
                        proceed = false;
                    }
                }

                // Strip is closed here. Items have their location.
                availableSpace = remainingSpace; // Regardless if due to recovery or no more items
                placed.AddRange(currentStrip);
            }

            // Once the top level items are placed repeat recursively 
            foreach (var item in placed)
            {
                // TODO debug level
                // Draw the items or sub rects
                var layout = GetLayout(item);
                Debug.Assert(!layout.Rect.IsEmpty);
                Squarify(layout.Rect, item);

                //OnlyDrawNoRecursion(item.Layout.Rect, item);
            }
        }

        private void Squarify(Rect availableSpace, IHierarchicalData data)
        {
            if (DebugEnabled)
            {
                // Print the item and the available space.
                _level++;
                Debug.WriteLine(GetLevelIndent() + "Squarify - " + data.Name + " Available=" + Format(availableSpace));
            }

            if (data.Children.Count == 0)
            {
                // Draw leaf node at its location
                //DrawRectangle(availableSpace, data); No, just calculate the layout!

                if (DebugEnabled)
                {
                    var layout = GetLayout(data);
                    Debug.WriteLine(GetLevelIndent() + "Printing " + data.Name + ", " + "Rect=" + Format(layout.Rect));
                }

                _level--;
                return;
            }

            var itemsToArrange = new List<IHierarchicalData>(data.Children);
            Squarify(availableSpace, itemsToArrange);
            _level--;
        }


        /// <summary>
        /// Returns the worst (largest) rectangle aspect ratio found within the strip members
        /// </summary>
        private double WorstRatio(IEnumerable<IHierarchicalData> stripMembers)
        {
            // Empty list ist worst possible
            var worst = double.MaxValue;

            foreach (var item in stripMembers)
            {
                var layout = GetLayout(item);
                worst = Math.Max(layout.Rect.Width / layout.Rect.Height,
                                 layout.Rect.Height / layout.Rect.Width);
            }

            return worst;
        }
    }
}