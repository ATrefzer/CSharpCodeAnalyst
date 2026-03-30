using System.Windows;
using System.Windows.Media;

namespace CSharpCodeAnalyst.Wpf;

public static class VisualTreeFinder
{
    public static T? FindChildByName<T>(this DependencyObject? parent, string name) where T : FrameworkElement
    {
        if (parent == null)
        {
            return null;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is T element && element.Name == name)
            {
                return element;
            }

            var result = FindChildByName<T>(child, name);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}