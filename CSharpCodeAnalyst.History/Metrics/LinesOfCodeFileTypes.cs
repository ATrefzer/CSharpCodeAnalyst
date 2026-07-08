namespace CSharpCodeAnalyst.History.Metrics;

public static class LinesOfCodeFileTypes
{
    // Shared, stateless region styles reused across languages that use plain double-/single-quoted
    // strings with C-style backslash escaping (i.e. all of them, so far).
    private static readonly DelimitedRegionStyle DoubleQuoteString = new()
    {
        Start = "\"", End = "\"", Escaping = EscapeStyle.Backslash, Kind = RegionKind.Code
    };

    private static readonly DelimitedRegionStyle SingleQuoteString = new()
    {
        Start = "'", End = "'", Escaping = EscapeStyle.Backslash, Kind = RegionKind.Code
    };

    // Markup attribute values: XML/HTML have no '\' escape mechanism, so a trailing backslash
    // (e.g. Path="C:\Temp\") must not swallow the closing quote.
    private static readonly DelimitedRegionStyle XmlDoubleQuoteString = new()
    {
        Start = "\"", End = "\"", Escaping = EscapeStyle.None, Kind = RegionKind.Code
    };

    private static readonly DelimitedRegionStyle XmlSingleQuoteString = new()
    {
        Start = "'", End = "'", Escaping = EscapeStyle.None, Kind = RegionKind.Code
    };

    /// <summary>
    ///     Whether the '"' at quoteIndex is preceded by a verbatim-string prefix: @" / $@" / @$".
    /// </summary>
    private static bool IsVerbatimStringPrefix(string line, int quoteIndex)
    {
        if (quoteIndex >= 1 && line[quoteIndex - 1] == '@')
        {
            return true;
        }

        return quoteIndex >= 2 && line[quoteIndex - 1] == '$' && line[quoteIndex - 2] == '@';
    }

    public static Dictionary<string, FileTypeInfo> GetFileTypes()
    {
        var fileTypes = new Dictionary<string, FileTypeInfo>();

        // C#
        fileTypes[".cs"] = new FileTypeInfo
        {
            Name = "C#",
            SingleLineComment = "//",
            Regions =
            {
                // Order is important
                new DelimitedRegionStyle { Start = "/*", End = "*/", Kind = RegionKind.Comment },
                // Raw string literals: any run of 3+ '"', closing run >= opening length. Must
                // come before the plain-string entry below, since both start with '"'. Must NOT
                // match after a verbatim prefix: @""" is a verbatim string starting with an
                // escaped quote (raw strings cannot have an @ prefix), and treating it as a raw
                // string would miss the real closing quote.
                new DelimitedRegionStyle
                {
                    Start = "\"", VariableLengthQuoteRun = true, Kind = RegionKind.Code,
                    RequiresPrefix = (line, i) => !IsVerbatimStringPrefix(line, i)
                },
                // Verbatim strings: @"...", $@"...", @$"...". Must also come before the
                // plain-string entry, since both start with '"'.
                new DelimitedRegionStyle
                {
                    Start = "\"", End = "\"", Escaping = EscapeStyle.DoubledDelimiter, Kind = RegionKind.Code,
                    RequiresPrefix = IsVerbatimStringPrefix
                },
                DoubleQuoteString,
                SingleQuoteString
            }
        };

        // XML (xaml, xml, resx, etc.)
        var xmlContent = new FileTypeInfo
        {
            Name = "XAML/XML",
            SingleLineComment = null, // XML has no single-line comments
            Regions = { new DelimitedRegionStyle { Start = "<!--", End = "-->", Kind = RegionKind.Comment }, XmlDoubleQuoteString, XmlSingleQuoteString }
        };

        fileTypes[".xaml"] = xmlContent;
        fileTypes[".xml"] = xmlContent;
        fileTypes[".resx"] = xmlContent;

        // HTML - comment syntax is XML-style. Note: this does NOT switch into JS/CSS comment
        // syntax inside embedded <script>/<style> blocks (would need tag-awareness, which this
        // line scanner does not have), so LOC inside such blocks is only approximate - the same
        // accepted trade-off as the rest of this file.
        // No single-quote region for HTML: apostrophes in prose ("don't") are far more common
        // than single-quoted attribute values containing "<!--", and an unpaired apostrophe
        // would open a never-closing region that swallows all following comments.
        fileTypes[".html"] = new FileTypeInfo
        {
            Name = "HTML",
            SingleLineComment = null,
            Regions = { new DelimitedRegionStyle { Start = "<!--", End = "-->", Kind = RegionKind.Comment }, XmlDoubleQuoteString }
        };
        fileTypes[".htm"] = fileTypes[".html"];

        // C++
        fileTypes[".cpp"] = new FileTypeInfo
        {
            Name = "C++",
            SingleLineComment = "//",
            Regions = { new DelimitedRegionStyle { Start = "/*", End = "*/", Kind = RegionKind.Comment }, DoubleQuoteString, SingleQuoteString }
        };

        fileTypes[".h"] = new FileTypeInfo
        {
            Name = "C++ Header",
            SingleLineComment = "//",
            Regions = { new DelimitedRegionStyle { Start = "/*", End = "*/", Kind = RegionKind.Comment }, DoubleQuoteString, SingleQuoteString }
        };

        // Python. Triple-quoted strings ('''...''' and """..."""") are treated as comments - this
        // is a deliberate simplification for docstrings (see Documentation/Notes), which also
        // means a plain multi-line string that happens to use triple quotes is miscounted as a
        // comment. Must come before the plain-quote entries below, since both share their
        // respective opening character.
        fileTypes[".py"] = new FileTypeInfo
        {
            Name = "Python",
            SingleLineComment = "#",
            Regions =
            {
                new DelimitedRegionStyle { Start = "'''", End = "'''", Kind = RegionKind.Comment },
                new DelimitedRegionStyle { Start = "\"\"\"", End = "\"\"\"", Kind = RegionKind.Comment },
                DoubleQuoteString,
                SingleQuoteString
            }
        };

        // JavaScript / TypeScript - identical comment and string syntax at this level of detail.
        var scriptContent = new FileTypeInfo
        {
            Name = "JavaScript/TypeScript",
            SingleLineComment = "//",
            Regions =
            {
                new DelimitedRegionStyle { Start = "/*", End = "*/", Kind = RegionKind.Comment },
                // Template literals: `...`, incl. multi-line and ${...} interpolation. Treated
                // like a regular string; the content of a ${...} hole is not parsed specially.
                new DelimitedRegionStyle { Start = "`", End = "`", Escaping = EscapeStyle.Backslash, Kind = RegionKind.Code },
                DoubleQuoteString,
                SingleQuoteString
            }
        };

        fileTypes[".js"] = scriptContent;
        fileTypes[".ts"] = scriptContent;

        // Java
        fileTypes[".java"] = new FileTypeInfo
        {
            Name = "Java",
            SingleLineComment = "//",
            Regions =
            {
                new DelimitedRegionStyle { Start = "/*", End = "*/", Kind = RegionKind.Comment },
                // Text blocks (Java 15+): always exactly three quotes, '\' escapes apply inside.
                // Must come before the plain-string entry below, since both start with '"'.
                new DelimitedRegionStyle { Start = "\"\"\"", End = "\"\"\"", Escaping = EscapeStyle.Backslash, Kind = RegionKind.Code },
                DoubleQuoteString,
                SingleQuoteString
            }
        };

        // CSS - only block comments; "//" is NOT a comment in standard CSS. Strings (in url(),
        // content:, attribute selectors) use '\' escaping per the CSS spec.
        fileTypes[".css"] = new FileTypeInfo
        {
            Name = "CSS",
            SingleLineComment = null,
            Regions =
            {
                new DelimitedRegionStyle { Start = "/*", End = "*/", Kind = RegionKind.Comment },
                DoubleQuoteString,
                SingleQuoteString
            }
        };

        // Plain text - no comment syntax and no delimited regions at all, so every non-blank
        // line is simply content. The simplest possible FileTypeInfo.
        fileTypes[".txt"] = new FileTypeInfo
        {
            Name = "Text"
        };

        return fileTypes;
    }
}