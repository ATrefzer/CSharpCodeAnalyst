using System.Globalization;
using System.Text;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Mcp.Contracts;

namespace CSharpCodeAnalyst.Mcp.Tools;

/// <summary>
///     One place that decides how a code element is written down, so every tool answers in the same
///     shape and a caller only has to learn it once.
///     <para>
///         The format is a compromise between two readers. A human skimming a transcript wants the name
///         first; the model needs the id to ask a follow-up question. Both are
///         on one line.
///     </para>
/// </summary>
internal static class ElementFormatter
{
    /// <summary>
    ///     A single element, as used in lists: kind, full path, id, and where it is defined.
    ///     Example: <c>[Class] Sample.Core.OrderService  id=8f3c...  Core/Orders/OrderService.cs:42</c>
    /// </summary>
    public static string Line(CodeElement element, string? sourceRoot)
    {
        var text = new StringBuilder();
        text.Append('[').Append(element.ElementType).Append("] ");
        text.Append(element.FullName);
        text.Append("  id=").Append(element.Id);

        var location = FirstLocation(element, sourceRoot);
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
    ///     Where an element is declared, as <c>path:line</c>. Null when the producer supplied no
    ///     location, which is the normal case for external elements and for several importers.
    ///     <para>
    ///         An element declared in several files - a partial class, a type whose members are spread
    ///         out - reports only the first. That is a lead, not the whole truth; describe_element lists
    ///         them all.
    ///     </para>
    /// </summary>
    public static string? FirstLocation(CodeElement element, string? sourceRoot)
    {
        var location = element.SourceLocations.FirstOrDefault();
        return location is null ? null : Location(location, sourceRoot);
    }

    /// <summary>
    ///     A source location as <c>path:line</c>, relative to the root of the graph it came from. The
    ///     directory has to stay: file names repeat across a code base - seven <c>Analyzer.cs</c> in
    ///     this one - so the name alone names no file. Only the shared prefix is dropped, which is the
    ///     part that is identical on every line of a result and therefore carries nothing.
    ///     Null when the location has no file, which the graph model permits.
    /// </summary>
    public static string? Location(SourceLocation location, string? sourceRoot)
    {
        if (location.File is null)
        {
            return null;
        }

        var file = SourcePaths.MakeRelative(location.File, sourceRoot);
        return $"{file}:{location.Line.ToString(CultureInfo.InvariantCulture)}";
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
    ///     <para>
    ///         <paramref name="hint" /> carries the way back to the missing entries, and it is a parameter
    ///         because that way differs per caller: a truncated search can be repeated with a higher limit,
    ///         a truncated list of children cannot - nothing about it is a search yet. Telling a caller to
    ///         narrow a question it has no means to narrow is the same dead end as saying nothing.
    ///     </para>
    /// </summary>
    public static void AppendLimited(StringBuilder text, IReadOnlyList<CodeElement> elements,
        string? sourceRoot, int limit, string? hint = null)
    {
        foreach (var element in elements.Take(limit))
        {
            text.Append("  ").AppendLine(Line(element, sourceRoot));
        }

        if (elements.Count > limit)
        {
            var omitted = elements.Count - limit;
            text.Append("  ... ")
                .Append(omitted.ToString(CultureInfo.InvariantCulture))
                .Append(" more not shown (")
                .Append(elements.Count.ToString(CultureInfo.InvariantCulture))
                .Append(" in total). ")
                .AppendLine(hint ?? "Raise the limit or ask a narrower question.");
        }
    }
}
