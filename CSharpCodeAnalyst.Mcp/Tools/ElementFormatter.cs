using System.Globalization;
using System.Text;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.Mcp.Tools;

/// <summary>
///     One place that decides how a code element is written down, so every tool answers in the same
///     shape and a caller only has to learn it once.
///     <para>
///         The format is a compromise between two readers. A human skimming a transcript wants the name
///         first; the model needs the id, because nothing else lets it ask a follow up question. Both are
///         on one line, because a block per element would spend most of the answer on structure.
///     </para>
/// </summary>
internal static class ElementFormatter
{
    /// <summary>
    ///     A single element, as used in lists: kind, full path, id, and where it is defined.
    ///     Example: <c>[Class] Sample.Core.OrderService  id=8f3c...  Orders.cs:42</c>
    /// </summary>
    public static string Line(CodeElement element)
    {
        var text = new StringBuilder();
        text.Append('[').Append(element.ElementType).Append("] ");
        text.Append(element.FullName);
        text.Append("  id=").Append(element.Id);

        var location = FirstLocation(element);
        if (location is not null)
        {
            text.Append("  ").Append(location);
        }

        if (element.IsExternal)
        {
            text.Append("  [external]");
        }

        if (element.IsGenerated)
        {
            text.Append("  [generated]");
        }

        return text.ToString();
    }

    /// <summary>
    ///     A source location as <c>file:line</c>, with the directory dropped - the file name plus the line
    ///     is what a reader needs to find the code, while full paths are long and identical across most of
    ///     a result. Null when the producer supplied no location, which is the normal case for external
    ///     elements and for several importers.
    /// </summary>
    public static string? FirstLocation(CodeElement element)
    {
        var location = element.SourceLocations.FirstOrDefault();
        if (location?.File is null)
        {
            return null;
        }

        var fileName = Path.GetFileName(location.File);
        return $"{fileName}:{location.Line.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    ///     Counts per kind, ordered by count, as <c>8 Calls, 3 Uses, 1 Inherits</c>. Gives a caller the
    ///     shape of a result before it reads the entries - and often that is already the answer.
    /// </summary>
    public static string Summarize<T>(IEnumerable<T> items, Func<T, string> kind)
    {
        var counts = items
            .GroupBy(kind)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
                $"{group.Count().ToString(CultureInfo.InvariantCulture)} {group.Key}");

        return string.Join(", ", counts);
    }

    /// <summary>
    ///     Appends at most <paramref name="limit" /> lines and says plainly how many were left out.
    ///     A silently truncated list is worse than a short one: a caller that cannot tell it is looking at
    ///     a fragment will happily conclude "there are only three callers".
    /// </summary>
    public static void AppendLimited(StringBuilder text, IReadOnlyList<CodeElement> elements, int limit)
    {
        foreach (var element in elements.Take(limit))
        {
            text.Append("  ").AppendLine(Line(element));
        }

        if (elements.Count > limit)
        {
            var omitted = elements.Count - limit;
            text.Append("  ... ")
                .Append(omitted.ToString(CultureInfo.InvariantCulture))
                .Append(" more not shown (")
                .Append(elements.Count.ToString(CultureInfo.InvariantCulture))
                .AppendLine(" in total). Narrow the question to see them.");
        }
    }
}
