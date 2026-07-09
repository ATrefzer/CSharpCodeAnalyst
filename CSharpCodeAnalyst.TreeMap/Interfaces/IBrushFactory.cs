using System.Windows.Media;

namespace CSharpCodeAnalyst.TreeMap.Interfaces;

   
public interface IBrushFactory
{
    SolidColorBrush GetBrush(string name);
}