using System.Globalization;
using System.Text;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.Mcp.Tools;

/// <summary>
///     Writes down relationships around a known element.
/// </summary>
internal static class RelationshipFormatter
{
    public static void Append(StringBuilder text, CodeGraph.Graph.CodeGraph graph,
        IReadOnlyList<Relationship> relationships, CodeElement anchor, bool anchorIsSource,
        string? sourceRoot, int limit)
    {
        var lines = relationships
            .Select(relationship => Describe(graph, relationship, anchor, anchorIsSource, sourceRoot))
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

    /// <summary>
    ///     Writes one relationship from the anchor's point of view. A relationship is a directed edge,
    ///     but the question was asked about one element, so <paramref name="anchorIsSource" /> decides
    ///     which end is ours (near) and which is the one worth naming (far). The arrow follows that
    ///     perspective rather than the direction of the edge, so every line reads the same way: our
    ///     side, arrow, their side.
    ///     <para>
    ///         The near element is named only when it is not the anchor itself - it repeats the header
    ///         line otherwise. That happens on a deep query, where the relationships hang off the
    ///         contained elements: without the name the caller sees that the class depends on
    ///         something, but not through which member.
    ///     </para>
    /// </summary>
    private static string? Describe(CodeGraph.Graph.CodeGraph graph, Relationship relationship,
        CodeElement anchor, bool anchorIsSource, string? sourceRoot)
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
        text.Append(ElementFormatter.Line(far, sourceRoot));

        // The relationship's own location is the call site, which is more specific than the
        // declaration of the far element that ElementFormatter already printed.
        var site = relationship.SourceLocations.FirstOrDefault();
        if (site is not null)
        {
            var formatted = ElementFormatter.Location(site, sourceRoot);
            if (formatted is not null)
            {
                text.Append("  at ").Append(formatted);
            }
        }

        return text.ToString();
    }
}
