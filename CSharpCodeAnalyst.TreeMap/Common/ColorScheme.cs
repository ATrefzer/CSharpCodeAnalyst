using System.Globalization;
using System.Runtime.Serialization;
using System.Windows.Media;
using CSharpCodeAnalyst.TreeMap.Interfaces;

namespace CSharpCodeAnalyst.TreeMap.Common
{
    /// <summary>
    /// Maps color keys to brushes.
    /// You can provide the mappings in the constructor or
    /// add keys on the fly. This will assign (limited) number of known colors
    /// to the next key.
    /// </summary>
    [DataContract]
    public sealed class ColorScheme : IColorScheme
    {
        /// <summary>
        /// All known names an the associated color argb
        /// </summary>
        [DataMember]
        private readonly Dictionary<string, int> _nameToArgb = new Dictionary<string, int>();

        [DataMember]
        private int[] _defaultColors;

        public ColorScheme()
        {
            CreateColorsDefinitions();
        }

        public ColorScheme(IEnumerable<string> names)
        {
            CreateColorsDefinitions();
            foreach (var name in names.OrderBy(name => name))
            {
                AssignFreeColor(name);
            }
        }

        public void Update(IEnumerable<ColorMapping> update)
        {
            foreach (var mapping in update)
            {
                // Ensure all used colors exist
                AddColor(mapping.Color);
                AssignColor(mapping);
            }
        }

        public SolidColorBrush GetBrush(string name)
        {
            if (!_nameToArgb.TryGetValue(name, out var argb))
            {
                return Brushes.Black;
            }

            return BrushCache.GetBrush(ColorConverter.FromArgb(argb));
        }

        public IEnumerable<ColorMapping> GetColorMappings()
        {
            return _nameToArgb.Select(pair => new ColorMapping { Name = pair.Key, Color = ColorConverter.FromArgb(pair.Value) });
        }

        public IEnumerable<Color> GetAllColors()
        {
            return _defaultColors.Select(ColorConverter.FromArgb);
        }

        public List<string> Names => _nameToArgb.Keys.ToList();

        public bool AddColor(Color newColor)
        {
            var argb = ColorConverter.ToArgb(newColor);
            if (_defaultColors.Contains(argb))
            {
                return false;
            }

            var newIndex = _defaultColors.Length;
            var tmp = new int[_defaultColors.Length + 1];
            _defaultColors.CopyTo(tmp, 0);
            tmp[newIndex] = argb;
            _defaultColors = tmp;
            return true;
        }

        public bool IsKnown(string alias)
        {
            return _nameToArgb.ContainsKey(alias);
        }

        /// <summary>
        /// Defines a predefined set of colors that can be distinguished by the eye.
        /// However the colors may not be enough. So a default color is assigned to all remaining keys.
        /// </summary>
        private void CreateColorsDefinitions()
        {
            var paletteA = new[]
                           {
                                   /*"FF000000", "FFFFFF00",*/ "FF1CE6FF", "FFFF34FF", "FFFF4A46", "FF008941", "FF006FA6", "FFA30059",
                                   "FFFFDBE5", "FF7A4900", "FF0000A6", "FF63FFAC", "FFB79762", "FF004D43", "FF8FB0FF", "FF997D87",
                                   "FF5A0007", "FF809693", "FFFEFFE6", "FF1B4400", "FF4FC601", "FF3B5DFF", "FF4A3B53", "FFFF2F80",
                                   "FF61615A", "FFBA0900", "FF6B7900", "FF00C2A0", "FFFFAA92", "FFFF90C9", "FFB903AA", "FFD16100",
                                   "FFDDEFFF", "FF000035", "FF7B4F4B", "FFA1C299", "FF300018", "FF0AA6D8", "FF013349", "FF00846F",
                                   "FF372101", "FFFFB500", "FFC2FFED", "FFA079BF", "FFCC0744", "FFC0B9B2", "FFC2FF99", "FF001E09",
                                   "FF00489C", "FF6F0062", "FF0CBD66", "FFEEC3FF", "FF456D75", "FFB77B68", "FF7A87A1", "FF788D66",
                                   "FF885578", "FFFAD09F", "FFFF8A9A", "FFD157A0", "FFBEC459", "FF456648", "FF0086ED", "FF886F4C",
                                   "FF34362D", "FFB4A8BD", "FF00A6AA", "FF452C2C", "FF636375", "FFA3C8C9", "FFFF913F", "FF938A81",
                                   "FF575329", "FF00FECF", "FFB05B6F", "FF8CD0FF", "FF3B9700", "FF04F757", "FFC8A1A1", "FF1E6E00",
                                   "FF7900D7", "FFA77500", "FF6367A9", "FFA05837", "FF6B002C", "FF772600", "FFD790FF", "FF9B9700",
                                   "FF549E79", "FFFFF69F", "FF201625", "FF72418F", "FFBC23FF", "FF99ADC0", "FF3A2465", "FF922329",
                                   "FF5B4534", "FFFDE8DC", "FF404E55", "FF0089A3", "FFCB7E98", "FFA4E804", "FF324E72", "FF6A3A4C",
                                   "FF83AB58", "FF001C1E", "FFD1F7CE", "FF004B28", "FFC8D0F6", "FFA3A489", "FF806C66", "FF222800",
                                   "FFBF5650", "FFE83000", "FF66796D", "FFDA007C", "FFFF1A59", "FF8ADBB4", "FF1E0200", "FF5B4E51",
                                   "FFC895C5", "FF320033", "FFFF6832", "FF66E1D3", "FFCFCDAC", "FFD0AC94", "FF7ED379", "FF012C58"
                           };

            // http://stackoverflow.com/questions/309149/generate-distinctly-different-rgb-colors-in-graphs

            var paletteB = new[]
                           {
                                   /*"FF000000," */ "FF00FF00", "FF0000FF", "FFFF0000", "FF01FFFE", "FFFFA6FE", "FFFFDB66", "FF006401",
                                   "FF010067", "FF95003A", "FF007DB5", "FFFF00F6", "FFFFEEE8", "FF774D00", "FF90FB92", "FF0076FF",
                                   "FFD5FF00", "FFFF937E", "FF6A826C", "FFFF029D", "FFFE8900", "FF7A4782", "FF7E2DD2", "FF85A900",
                                   "FFFF0056", "FFA42400", "FF00AE7E", "FF683D3B", "FFBDC6FF", "FF263400", "FFBDD393", "FF00B917",
                                   "FF9E008E", "FF001544", "FFC28C9F", "FFFF74A3", "FF01D0FF", "FF004754", "FFE56FFE", "FF788231",
                                   "FF0E4CA1", "FF91D0CB", "FFBE9970", "FF968AE8", "FFBB8800", "FF43002C", "FFDEFF74", "FF00FFC6",
                                   "FFFFE502", "FF620E00", "FF008F9C", "FF98FF52", "FF7544B1", "FFB500FF", "FF00FF78", "FFFF6E41",
                                   "FF005F39", "FF6B6882", "FF5FAD4E", "FFA75740", "FFA5FFD2", "FFFFB167", "FF009BFF", "FFE85EBE"
                           };

            _defaultColors = paletteA.Select(argb => int.Parse(argb, NumberStyles.HexNumber)).ToArray();
        }

        /// <summary>
        /// Returns false if the name got a default color assigned.
        /// There are enough colors.
        /// </summary>
        public bool AssignFreeColor(string name)
        {
            var uniqueColor = false;

            if (_nameToArgb.ContainsKey(name))
            {
                throw new Exception("Name already exists");
            }

            var freeColors = _defaultColors.Except(_nameToArgb.Values).ToList();
            if (freeColors.Any())
            {
                _nameToArgb[name] = freeColors.First();
                uniqueColor = true;
            }
            else
            {
                // Not enough colors!
                _nameToArgb[name] = ColorConverter.ToArgb(DefaultDrawingPrimitives.DefaultColor);
            }

            return uniqueColor;
        }

        private void AssignColor(ColorMapping mapping)
        {
            var argb = ColorConverter.ToArgb(mapping.Color);
            _nameToArgb.Remove(mapping.Name);
            _nameToArgb.Add(mapping.Name, argb);
        }
    }
}