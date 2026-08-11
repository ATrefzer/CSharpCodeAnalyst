using System.ComponentModel;
using System.Globalization;
using System.Text;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeGraph.Search;
using CSharpCodeAnalyst.Mcp.Contracts;
using ModelContextProtocol.Server;

namespace CSharpCodeAnalyst.Mcp.Tools;

/// <summary>
///     Tools that work on a single code element.
/// </summary>
[McpServerToolType]
public sealed class ElementTools(ICodeGraphSnapshotSource snapshotSource)
{
    /// <summary>
    ///     A type with many members still fits; a namespace with hundreds of types does not, and listing
    ///     them all would bury the rest of the answer.
    /// </summary>
    private const int ChildLimit = 40;

    private const int LocationLimit = 5;

    private const int DefaultSearchLimit = 50;

    /// <summary>
    ///     A caller that asks for a thousand hits does not want to read them - it wants to be sure it
    ///     saw everything, and a truncation notice answers that better than a flooded context window.
    /// </summary>
    private const int MaxSearchLimit = 200;

    [McpServerTool(Name = "search_elements", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Finds code elements by name. This is the entry point for every other tool, because element " +
        "ids cannot be guessed.\n" +
        "Query syntax. An all-lowercase term matches anywhere in the full name, case-insensitively. " +
        "A term containing an uppercase letter switches to camel-hump matching: it is split at every " +
        "uppercase letter and the parts must occur in that order, each starting a word, matched " +
        "case-sensitively. So 'OS', 'OrdServ' and 'OrderService' all find 'OrderService', but 'OSvc' " +
        "finds nothing, because 'Svc' does not occur in the name.\n" +
        "The full name is the whole path down from the assembly, so a path prefix lists what sits " +
        "inside a container: 'sample.core.orders type:class' finds the classes under that namespace, " +
        "and a type's path finds its members. Nesting is included - the prefix of an outer namespace " +
        "also matches everything in the namespaces below it. Write a path term in lowercase; it is " +
        "then a plain substring, whereas one uppercase letter turns the whole term into a " +
        "case-sensitive camel-hump pattern that a path rarely survives.\n" +
        "Space means AND, '|' means OR, a leading '-' excludes. 'type:class' (also interface, struct, " +
        "record, method, property, field, event, enum, delegate, namespace, assembly) restricts the " +
        "kind; 'source:extern', 'source:intern' and 'source:generated' restrict the origin.\n" +
        "Example: 'order type:class -source:extern' finds classes in the analyzed code whose full " +
        "name contains 'order'.")]
    public async Task<string> SearchElementsAsync(
        [Description("Search expression, see the syntax above.")]
        string query,
        [Description("Maximum number of results (default 50, capped at 200).")]
        int limit = DefaultSearchLimit,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "The query is empty. Pass a name or a search expression.";
        }

        var snapshot = await snapshotSource.GetSnapshotAsync(cancellationToken);
        if (snapshot is null)
        {
            return ToolText.NoProjectLoaded;
        }

        var effectiveLimit = Math.Clamp(limit, 1, MaxSearchLimit);
        var expression = SearchExpressionFactory.CreateSearchExpression(query);

        var matches = snapshot.Graph.Nodes.Values
            .Where(expression.Evaluate)
            .OrderBy(element => element.IsExternal)
            .ThenBy(element => element.ElementType)
            .ThenBy(element => element.FullName, StringComparer.Ordinal)
            .ToList();

        if (matches.Count == 0)
        {
            return $"Nothing matches '{query}'. Note that the search runs over the graph, not over " +
                   "your files: anything the parser did not see is not in it, and external code is " +
                   "only present as far as it is referenced.";
        }

        var text = new StringBuilder();
        text.Append(matches.Count.ToString(CultureInfo.InvariantCulture))
            .Append(" match(es) for '").Append(query).Append("': ")
            .AppendLine(ElementFormatter.Summarize(matches, m => m.ElementType.ToString()));
        text.AppendLine();

        // "Raise the limit" is bad advice once the limit is already at the cap - the caller would spend
        // a call to get the identical answer back.
        var hint = effectiveLimit < MaxSearchLimit
            ? $"Raise limit (up to {MaxSearchLimit.ToString(CultureInfo.InvariantCulture)}) or add " +
              "terms to narrow the query."
            : "Add terms to narrow the query - the limit is already at its maximum.";

        ElementFormatter.AppendLimited(text, matches, snapshot.SourceRoot, effectiveLimit, hint);

        return text.ToString();
    }

    [McpServerTool(Name = "describe_element", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Everything known about one code element: kind, full path, accessibility, where it is defined, " +
        "what it contains, and how many relationships run in and out. Takes an id from search_elements.")]
    public async Task<string> DescribeElementAsync(
        [Description("Element id, as returned by search_elements.")]
        string id,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await snapshotSource.GetSnapshotAsync(cancellationToken);
        if (snapshot is null)
        {
            return ToolText.NoProjectLoaded;
        }

        var element = snapshot.Graph.TryGetCodeElement(id);
        if (element is null)
        {
            return ToolText.UnknownId(id);
        }

        var text = new StringBuilder();

        text.Append('[').Append(element.ElementType).Append("] ").AppendLine(element.FullName);
        text.Append("id: ").AppendLine(element.Id);

        AppendFlags(text, element);
        AppendLocations(text, element, snapshot.SourceRoot);
        AppendParents(text, element, snapshot.SourceRoot);
        AppendChildren(text, element, snapshot.SourceRoot);
        AppendRelationships(text, snapshot.Graph, element);

        return text.ToString();
    }

    private static void AppendFlags(StringBuilder text, CodeElement element)
    {
        // Unknown is not a value, it means the producer told us nothing - reporting it as an access
        // level would invite exactly the conclusion the model must not draw.
        if (element.AccessLevel != AccessLevel.Unknown)
        {
            text.Append("Access: ").AppendLine(element.AccessLevel.ToString());
        }

        if (element.IsExternal)
        {
            text.AppendLine(
                "External: defined outside the analyzed code. Its own dependencies were not analyzed, " +
                "so an empty outgoing result says nothing about it.");
        }

        if (element.IsGenerated)
        {
            text.AppendLine("Generated: written by a tool. Editing it by hand has no lasting effect.");
        }

        if (element.Attributes.Count > 0)
        {
            text.Append("Attributes: ").AppendLine(string.Join(", ", element.Attributes.Order()));
        }
    }

    private static void AppendLocations(StringBuilder text, CodeElement element, string? sourceRoot)
    {
        if (element.SourceLocations.Count == 0)
        {
            return;
        }

        text.AppendLine();
        text.AppendLine(element.SourceLocations.Count == 1 ? "Defined in:" : "Declarations:");

        foreach (var location in element.SourceLocations.Take(LocationLimit))
        {
            var formatted = ElementFormatter.Location(location, sourceRoot);
            if (formatted is not null)
            {
                text.Append("  ").AppendLine(formatted);
            }
        }

        if (element.SourceLocations.Count > LocationLimit)
        {
            var omitted = element.SourceLocations.Count - LocationLimit;
            text.Append("  ... ").Append(omitted.ToString(CultureInfo.InvariantCulture))
                .AppendLine(" more");
        }
    }

    private static void AppendParents(StringBuilder text, CodeElement element, string? sourceRoot)
    {
        var path = element.GetPathToRoot(false);
        if (path.Count == 0)
        {
            return;
        }

        text.AppendLine();
        text.AppendLine("Contained in:");
        foreach (var ancestor in path)
        {
            text.Append("  ").AppendLine(ElementFormatter.Line(ancestor, sourceRoot));
        }
    }

    private static void AppendChildren(StringBuilder text, CodeElement element, string? sourceRoot)
    {
        if (element.Children.Count == 0)
        {
            return;
        }

        var children = element.Children
            .OrderBy(child => child.ElementType)
            .ThenBy(child => child.Name, StringComparer.Ordinal)
            .ToList();

        text.AppendLine();
        text.Append("Contains (").Append(children.Count.ToString(CultureInfo.InvariantCulture))
            .Append("): ")
            .AppendLine(ElementFormatter.Summarize(children, child => child.ElementType.ToString()));

        // The way to the remaining children is a search over their shared prefix, because a full name
        // is the path down from the assembly and every child carries this element's. Lower case is not
        // cosmetic: it keeps the term a plain substring, while a single uppercase letter would make it
        // a case-sensitive camel-hump pattern and the path would stop matching itself.
        var hint = $"Use search_elements with '{element.FullName.ToLowerInvariant()}' to list them - " +
                   "that reports everything below this element, not only the direct children.";

        ElementFormatter.AppendLimited(text, children, sourceRoot, ChildLimit, hint);
    }

    private static void AppendRelationships(StringBuilder text, CodeGraph.Graph.CodeGraph graph,
        CodeElement element)
    {
        text.AppendLine();

        // Outgoing relationships live on the element itself.
        var outgoing = element.Relationships;
        if (outgoing.Count > 0)
        {
            text.Append("Outgoing relationships (")
                .Append(outgoing.Count.ToString(CultureInfo.InvariantCulture)).Append("): ")
                .AppendLine(ElementFormatter.Summarize(outgoing, r => r.Type.ToString()));
        }
        else
        {
            text.AppendLine("Outgoing relationships: none");
        }

        var incoming = graph.GetAllRelationships().Count(r => r.TargetId == element.Id);
        text.Append("Incoming relationships: ")
            .AppendLine(incoming.ToString(CultureInfo.InvariantCulture));

        text.AppendLine(
            "Use find_outgoing_relationships or find_incoming_relationships for the actual entries.");
    }
}
