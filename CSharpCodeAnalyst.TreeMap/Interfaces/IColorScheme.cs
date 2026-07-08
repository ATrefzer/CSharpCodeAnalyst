using System.Windows.Media;
using CSharpCodeAnalyst.TreeMap.Common;

namespace CSharpCodeAnalyst.TreeMap.Interfaces
{
    // TODO #alias cleanup Palette or Scheme?
    public interface IColorPalette
    {
        /// <summary>
        /// Returns all color mappings {name -> color}. The mappings can be updated
        /// </summary>
        IEnumerable<ColorMapping> GetColorMappings();

        void Update(IEnumerable<ColorMapping> update);

        /// <summary>
        /// Adds a new color to the color schema. The new color can be assigned to an existing name.
        /// </summary>
        bool AddColor(Color newColor);

        /// <summary>
        /// Returns all available colors. These are a bunch of default colors plus the user defined colors.
        /// </summary>
        IEnumerable<Color> GetAllColors();

        bool IsKnown(string alias);
    }
    
    public interface IBrushFactory
    {
        SolidColorBrush GetBrush(string name);
    }

    public interface IColorScheme : IBrushFactory, IColorPalette
    {

    }
}