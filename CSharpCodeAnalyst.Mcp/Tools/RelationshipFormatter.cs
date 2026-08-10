using System.Globalization;
using System.Text;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.Mcp.Tools;

/// <summary>
///     Writes down relationships around a known element.
///     <para>
///         One end of every relationship is the element the caller asked about, so spelling both ends
///         out in full would repeat the same name on every line. Only the far end gets its full name and
///         id - that is the one worth a follow up question. The near end appears only when it differs
///         from the anchor, which happens in the deep searches, where the relationship may start at a
///         member rather than at the element itself.
///     </para>
/// </summary>
internal static class RelationshipFormatter
{
    public static void Append(StringBuilder text, CodeGraph.Graph.CodeGraph graph,
        IReadOnlyList<Relationship> relationships, CodeElement anchor, bool anchorIsSource, int limit)
    {
        var lines = relationships
            .Select(relationship => Describe(graph, relationship, anchor, anchorIsSource))
            .Where(line => line is not null)
            .Select(line => line!)
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToList();

        foreach (var line in lines.Take(limit))
        {
            text.Append("  ").AppendLine(line);
        }

        if (lines.Count > limit)
        {
            var omitted = lines.Count - limit;
            text.Append("  ... ").Append(omitted.ToString(CultureInfo.InvariantCulture))
                .Append(" more not shown (")
                .Append(lines.Count.ToString(CultureInfo.InvariantCulture))
                .AppendLine(" in total). Raise the limit or ask a narrower question.");
        }
    }

    private static string? Describe(CodeGraph.Graph.CodeGraph graph, Relationship relationship,
        CodeElement anchor, bool anchorIsSource)
    {
        var nearId = anchorIsSource ? relationship.SourceId : relationship.TargetId;
        var farId = anchorIsSource ? relationship.TargetId : relationship.SourceId;

        var far = graph.TryGetCodeElement(farId);
        if (far is null)
        {
            // A relationship pointing at something the graph does not contain would be a defect
            // elsewhere. Skipping it keeps this tool from turning that into an exception.
            return null;
        }

        var text = new StringBuilder();
        text.Append(relationship.Type.ToString().PadRight(14));

        if (nearId != anchor.Id)
        {
            var near = graph.TryGetCodeElement(nearId);
            if (near is not null)
            {
                text.Append(near.Name).Append(' ');
            }
        }

        text.Append(anchorIsSource ? "-> " : "<- ");
        text.Append(ElementFormatter.Line(far));

        // The relationship's own location is the call site, which is more specific than the
        // declaration of the far element that ElementFormatter already printed.
        var site = relationship.SourceLocations.FirstOrDefault();
        if (site?.File is not null)
        {
            text.Append("  at ").Append(Path.GetFileName(site.File)).Append(':')
                .Append(site.Line.ToString(CultureInfo.InvariantCulture));
        }

        return text.ToString();
    }
}
