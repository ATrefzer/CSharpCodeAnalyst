using System.Collections.Concurrent;
using CSharpCodeAnalyst.CodeGraph.Contracts;

namespace CSharpCodeAnalyst.CodeParser.Parser;

public enum RegionKind
{
    Code,
    Comment
}

public enum EscapeStyle
{
    // The delimiter has no escape mechanism at all (block comments, C# raw strings).
    None,

    // '\' escapes the following character (regular strings, chars, template literals).
    Backslash,

    // Writing the delimiter twice in a row is the escape sequence for a literal delimiter
    // character; '\' has no special meaning (C# verbatim strings: "" inside @"...").
    DoubledDelimiter
}

/// <summary>
///     A delimited region within a source file: something that opens with one delimiter and
///     closes with another (or, for C# raw strings, a variable-length run of the same
///     character), during which the normal single-line-comment/region scanning rules do not
///     apply. Every "quoted string" or "block comment" variant in every supported language is
///     expressed as one of these - adding a new language-specific variant (e.g. a Go raw string
///     or a PHP heredoc) means adding a data entry in <see cref="FileTypeInfo.Regions" />, not a
///     new parser state.
/// </summary>
public class DelimitedRegionStyle
{
    public required string Start { get; init; }

    // Unused when VariableLengthQuoteRun is true - the closing run is dynamically the same
    // character as Start, with length >= MinimumQuoteRunLength.
    public string? End { get; init; }

    public EscapeStyle Escaping { get; init; } = EscapeStyle.None;
    public RegionKind Kind { get; init; } = RegionKind.Comment;

    // C# 11+ raw string literals: """...""", """"...."""", etc. Start holds the quote
    // character; whatever run length (>= MinimumQuoteRunLength) opens the region becomes the
    // required closing run length.
    public bool VariableLengthQuoteRun { get; init; }
    public int MinimumQuoteRunLength { get; init; } = 3;

    // Optional look-back predicate for delimiters that are only valid in certain contexts, e.g.
    // C# verbatim strings ('"' must be preceded by @ / $@ / @$).
    public Func<string, int, bool>? RequiresPrefix { get; init; }
}

public class FileTypeInfo
{
    public string Name { get; set; } = string.Empty;

    // Comments running to the end of the line, e.g. "//" or "#". Not delimiter-paired, so this
    // stays separate from Regions.
    public string? SingleLineComment { get; set; }

    // All delimited regions (block comments AND string/char/template-literal variants) for this
    // file type, checked in order - the first style whose opener matches at a position wins. More
    // specific/gated styles (raw strings, verbatim strings, language-specific comment styles)
    // must be listed before more general ones that share the same opening character (e.g. a
    // plain string), or the general one will shadow them.
    public List<DelimitedRegionStyle> Regions { get; } = [];
}

/// <summary>
///     Calculates the lines of code and comments over all files in a directory.
///     This a more simple approach that using Roslyn but in works for different file types.
///     You can register a custom handler for dedicated file types.
/// </summary>
public class LinesOfCodeProvider
{
    private readonly Dictionary<string, FileTypeInfo> _fileTypes;
    private readonly Dictionary<string, Func<string, (int code, int comments)>> _handlers = [];

    private readonly Lock _lock = new();
    private readonly IProgress? _progress;

    public LinesOfCodeProvider(IProgress? progress)
    {
        _progress = progress;
        _fileTypes = LinesOfCodeFileTypes.GetFileTypes();
    }

    /// <summary>
    ///     Extension point for callers who want to add or override file type
    ///     definitions before analysis starts.
    /// </summary>
    public void RegisterCustomProvider(string extension, Func<string, (int, int)> handler)
    {
        _handlers[extension.ToLowerInvariant()] = handler;
    }


    private void SendProgress(int processedFiles)
    {
        if (processedFiles % 10 == 0)
        {
            var msg = $"Already processed {processedFiles} files.";
            _progress?.SendProgress(msg);
        }
    }

    public class LinesOfCode
    {
        public int Code { get; init; }
        public int Comments { get; init; }
    }
    /// <summary>
    ///     Public entry point: recursively analyzes all recognized source files
    ///     under the given directory and returns per-file code/comment/blank counts.
    /// </summary>
    public Dictionary<string, LinesOfCode> AnalyzeDirectory(string path)
    {
        var processedFiles = 0;

        _progress?.SendProgress($"Collecting files from '{path}'");

        var results = new ConcurrentDictionary<string, LinesOfCode>();

        var files = SafeEnumerateFiles(path)
            .Where(f => _fileTypes.ContainsKey(Path.GetExtension(f).ToLowerInvariant()));

        Parallel.ForEach(files, file =>
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();

            try
            {
                if (_handlers.TryGetValue(ext, out var handler))
                {
                    // Override processing with custom handler
                    var stats = handler(file);
                    results[file] = new LinesOfCode{Code =stats.code, Comments = stats.comments};
                }
                else
                {
                    var stats = AnalyzeFile(file, _fileTypes[ext]);
                    results[file] = new LinesOfCode{Code =stats.code, Comments = stats.comments};
                }

                int processed;
                lock (_lock)
                {
                    processedFiles++;
                    processed = processedFiles;
                }

                SendProgress(processed);
            }
            catch (IOException)
            {
                // Skip files that can't be read (locked, race with deletion, etc.)
            }
            catch (UnauthorizedAccessException)
            {
                // Skip files/directories we don't have permission to read
            }
        });

        return new Dictionary<string, LinesOfCode>(results);
    }

    /// <summary>
    ///     Enumerates files recursively while tolerating inaccessible subdirectories,
    ///     instead of letting one bad folder abort the entire scan.
    /// </summary>
    private static IEnumerable<string> SafeEnumerateFiles(string path)
    {
        var result = new List<string>();
        var pending = new Stack<string>();
        pending.Push(path);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            string[] subDirs;
            string[] files;

            try
            {
                files = Directory.GetFiles(current);
                subDirs = Directory.GetDirectories(current);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            result.AddRange(files);

            foreach (var dir in subDirs)
            {
                var folderName = Path.GetFileName(dir);
                if (!folderName.StartsWith("."))
                {
                    pending.Push(dir);
                }
            }
        }

        return result;
    }

    private static bool MatchesAt(string line, int index, string token)
    {
        if (index + token.Length > line.Length)
        {
            return false;
        }

        for (var i = 0; i < token.Length; i++)
        {
            if (line[index + i] != token[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Checks whether a region opens at the given position. Regions are checked in the order
    ///     declared on the FileTypeInfo, and the first one whose opener matches - and whose
    ///     optional RequiresPrefix predicate holds - wins.
    /// </summary>
    private static RegionMatch? TryMatchRegionOpen(string line, int index, FileTypeInfo info)
    {
        foreach (var style in info.Regions)
        {
            if (style.RequiresPrefix != null && !style.RequiresPrefix(line, index))
            {
                continue;
            }

            if (style.VariableLengthQuoteRun)
            {
                if (line[index] != style.Start[0])
                {
                    continue;
                }

                var runLength = CountConsecutiveQuotes(line, index, style.Start[0]);
                if (runLength >= style.MinimumQuoteRunLength)
                {
                    return new RegionMatch(style, runLength);
                }

                continue;
            }

            if (MatchesAt(line, index, style.Start))
            {
                return new RegionMatch(style, style.Start.Length);
            }
        }

        return null;
    }

    /// <summary>
    ///     Counts how many consecutive occurrences of quoteChar start at the given index. Used to
    ///     detect and close variable-length quote-run delimiters (C# raw strings: """, """", ...),
    ///     which can be any run of 3+ quotes, as long as the closing run is at least as long as
    ///     the opening one.
    /// </summary>
    private static int CountConsecutiveQuotes(string line, int index, char quoteChar)
    {
        var count = 0;
        while (index + count < line.Length && line[index + count] == quoteChar)
        {
            count++;
        }

        return count;
    }

    /// <summary>
    ///     Dispatches to the step logic for the region's delimiter kind. This is the one place
    ///     that knows about the different escaping/matching strategies; the caller in
    ///     AnalyzeLinesCore just applies the resulting Length/ClosesRegion uniformly.
    /// </summary>
    private static RegionStep EvaluateRegionChar(string line, int index, DelimitedRegionStyle region, int openLength)
    {
        if (region.VariableLengthQuoteRun)
        {
            return EvaluateQuoteRun(line, index, region.Start[0], openLength);
        }

        // Every non-VariableLengthQuoteRun region has a non-null End by construction.
        var end = region.End!;
        return region.Escaping switch
        {
            EscapeStyle.Backslash => EvaluateBackslashEscaped(line, index, end),
            EscapeStyle.DoubledDelimiter => EvaluateDoubledDelimiter(line, index, end),
            _ => EvaluatePlain(line, index, end)
        };
    }

    /// <summary>
    ///     C# raw strings: the region closes on a run of the same quote character that is at
    ///     least as long as the opening run (requiredLength); a shorter run is just content.
    /// </summary>
    private static RegionStep EvaluateQuoteRun(string line, int index, char quoteChar, int requiredLength)
    {
        if (line[index] != quoteChar)
        {
            return new RegionStep(1, false);
        }

        var runLength = CountConsecutiveQuotes(line, index, quoteChar);
        return runLength >= requiredLength
            ? new RegionStep(requiredLength, true)
            : new RegionStep(runLength, false);
    }

    /// <summary>
    ///     Regular strings/chars/template literals: '\' escapes the following character, the
    ///     region otherwise closes on the next occurrence of End.
    /// </summary>
    private static RegionStep EvaluateBackslashEscaped(string line, int index, string end)
    {
        if (line[index] == '\\' && index + 1 < line.Length)
        {
            return new RegionStep(2, false);
        }

        return MatchesAt(line, index, end) ? new RegionStep(end.Length, true) : new RegionStep(1, false);
    }

    /// <summary>
    ///     C# verbatim strings: End written twice in a row is the escape sequence for a literal,
    ///     embedded delimiter character and keeps the region open; a single End closes it. '\'
    ///     has no special meaning.
    /// </summary>
    private static RegionStep EvaluateDoubledDelimiter(string line, int index, string end)
    {
        if (!MatchesAt(line, index, end))
        {
            return new RegionStep(1, false);
        }

        return MatchesAt(line, index + end.Length, end)
            ? new RegionStep(end.Length * 2, false)
            : new RegionStep(end.Length, true);
    }

    /// <summary>
    ///     Block comments and comment-style multi-quote delimiters: no escaping at all, the
    ///     region closes on the literal End sequence.
    /// </summary>
    private static RegionStep EvaluatePlain(string line, int index, string end)
    {
        return MatchesAt(line, index, end) ? new RegionStep(end.Length, true) : new RegionStep(1, false);
    }

    /// <summary>
    ///     Analyzes an in-memory set of lines for the given file extension.
    ///     Exists mainly to make the parser unit-testable without touching disk.
    /// </summary>
    public (int Code, int Comments) AnalyzeLines(IReadOnlyList<string> lines, string extension)
    {
        var ext = extension.ToLowerInvariant();
        if (!_fileTypes.TryGetValue(ext, out var info))
        {
            throw new ArgumentException($"Unsupported file extension: {extension}", nameof(extension));
        }

        return AnalyzeLinesCore(lines, info);
    }

    private (int code, int comments) AnalyzeFile(string path, FileTypeInfo info)
    {
        var lines = File.ReadAllLines(path);
        return AnalyzeLinesCore(lines, info);
    }

    private (int code, int comments) AnalyzeLinesCore(IReadOnlyList<string> lines, FileTypeInfo info)
    {
        int codeLines = 0, commentLines = 0, blankLines = 0;
        var state = ParserState.Normal;
        DelimitedRegionStyle? activeRegion = null;
        var activeRegionCloseLength = 0; // opening delimiter length; for VariableLengthQuoteRun this is the required closing run length

        foreach (var line in lines)
        {
            // Empty line. Deliberately checked before the parser state: a blank-looking line
            // always counts as blank here, even if it lexically sits inside a multi-line raw/
            // verbatim string or block comment (common LOC-tool convention, e.g. cloc).
            // SourceMetricsCollector (the Roslyn-based counter for the same purpose) does the
            // opposite: it has no "blank" concept and attributes every line touched by a
            // token/trivia span to Code/Comment, so a blank-looking line embedded in a
            // multi-line string literal or comment is counted as Code/Comment there instead of
            // Blank here. This is the main source of the small per-file differences between the
            // two counters and is accepted, not a bug - see
            // ArchitecturalRules/Analyzer.cs GetSampleRules() for a real example (6 blank lines
            // inside a raw string literal).
            if (string.IsNullOrWhiteSpace(line))
            {
                blankLines++;
                continue;
            }

            var hasCode = false;
            var hasComment = false;

            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                var peek1 = i + 1 < line.Length ? line[i + 1] : '\0';

                switch (state)
                {
                    case ParserState.Normal:
                        // Single-line comment start?
                        if (info.SingleLineComment != null && c == info.SingleLineComment[0] &&
                            (info.SingleLineComment.Length == 1 || peek1 == info.SingleLineComment[1]))
                        {
                            hasComment = true;
                            i = line.Length; // rest of line is a comment
                            continue;
                        }

                        // Does a delimited region (block comment, string, char, template
                        // literal, raw/verbatim string, ...) start here?
                        var match = TryMatchRegionOpen(line, i, info);
                        if (match != null)
                        {
                            state = ParserState.InRegion;
                            activeRegion = match.Value.Style;
                            activeRegionCloseLength = match.Value.OpenLength;

                            if (activeRegion.Kind == RegionKind.Code)
                            {
                                hasCode = true;
                            }
                            else
                            {
                                hasComment = true;
                            }

                            // Advance past the opening delimiter; -1 because the enclosing
                            // for-loop will also do i++.
                            i += match.Value.OpenLength - 1;
                            continue;
                        }

                        // Regular code character
                        if (!char.IsWhiteSpace(c))
                        {
                            hasCode = true;
                        }

                        break;

                    case ParserState.InRegion:
                        var region = activeRegion!;
                        if (region.Kind == RegionKind.Code)
                        {
                            hasCode = true;
                        }
                        else
                        {
                            hasComment = true;
                        }

                        var step = EvaluateRegionChar(line, i, region, activeRegionCloseLength);
                        if (step.ClosesRegion)
                        {
                            state = ParserState.Normal;
                            activeRegion = null;
                        }

                        // -1 because the enclosing for-loop will also do i++.
                        i += step.Length - 1;
                        break;
                }
            }

            // Decide the line's category. A line containing both code and a
            // comment (e.g. "int x = 5; // note") is counted as code, which
            // matches common LOC-counting conventions.
            if (hasCode)
            {
                codeLines++;
            }
            else if (hasComment)
            {
                commentLines++;
            }
        }

        return (codeLines, commentLines);
    }

    /// <summary>
    ///     How far scanning advances for the character at the current position while inside a
    ///     region, and whether that character closes the region. A plain content character that
    ///     matches nothing special is Length=1, ClosesRegion=false - the common case.
    /// </summary>
    private readonly record struct RegionStep(int Length, bool ClosesRegion);

    private enum ParserState
    {
        Normal,
        InRegion
    }

    private readonly record struct RegionMatch(DelimitedRegionStyle Style, int OpenLength);
}