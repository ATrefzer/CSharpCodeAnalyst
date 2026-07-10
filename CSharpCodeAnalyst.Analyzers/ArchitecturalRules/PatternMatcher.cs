using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules;

public static class PatternMatcher
{
    /// <summary>
    ///     Resolves a pattern like "Business.**" to a set of CodeElement IDs
    ///     Pattern format: "Base.Path" or "Base.Path.*" or "Base.Path.**"
    /// </summary>
    public static HashSet<string> ResolvePattern(string pattern, CodeGraph.Graph.CodeGraph codeGraph)
    {
        var matchingIds = new HashSet<string>();

        if (string.IsNullOrEmpty(pattern))
            return matchingIds;

        // Split pattern into base path and wildcard suffix
        var (basePath, expansionMode) = ParsePattern(pattern);

        // A full path is not necessarily unique: overloaded methods share the same full name.
        // Resolve to ALL matching elements and union their expansions, so e.g. an ALLOW rule for
        // a method covers every overload instead of an arbitrary single one.
        foreach (var startElement in FindStartElements(basePath, codeGraph))
        {
            ApplyExpansion(startElement, expansionMode, matchingIds);
        }

        return matchingIds;
    }

    private static (string basePath, ExpansionMode mode) ParsePattern(string pattern)
    {
        if (pattern.EndsWith(".**"))
        {
            var basePath = pattern.Substring(0, pattern.Length - 3); // Remove ".**"
            return (basePath, ExpansionMode.Recursive);
        }

        if (pattern.EndsWith(".*"))
        {
            var basePath = pattern.Substring(0, pattern.Length - 2); // Remove ".*"
            return (basePath, ExpansionMode.DirectChildren);
        }

        // No wildcard suffix - just the element itself
        return (pattern, ExpansionMode.Self);
    }

    private static IEnumerable<CodeElement> FindStartElements(string basePath, CodeGraph.Graph.CodeGraph codeGraph)
    {
        // All elements with an exact FullName match (more than one for overloaded members).
        // The comparison is case-sensitive: C# identifiers are, so "MyApp.business" must not match
        // "MyApp.Business" - otherwise a typo silently hits the wrong element instead of raising the
        // no-match warning that is supposed to catch it.
        return codeGraph.Nodes.Values.Where(element =>
            string.Equals(element.FullName, basePath, StringComparison.Ordinal));
    }

    private static void ApplyExpansion(CodeElement startElement, ExpansionMode mode, HashSet<string> matchingIds)
    {
        switch (mode)
        {
            case ExpansionMode.Self:
                // Only the start element itself
                matchingIds.Add(startElement.Id);
                break;

            case ExpansionMode.DirectChildren:
                // Start element + direct children only
                matchingIds.Add(startElement.Id);
                foreach (var child in startElement.Children)
                {
                    matchingIds.Add(child.Id);
                }

                break;

            case ExpansionMode.Recursive:
                // Start element + all descendants recursively
                var allDescendants = startElement.GetChildrenIncludingSelf();
                foreach (var descendant in allDescendants)
                {
                    matchingIds.Add(descendant);
                }

                break;
        }
    }

    private enum ExpansionMode
    {
        Self, // No wildcard - just the element itself
        DirectChildren, // .* - element + direct children
        Recursive // .** - element + all descendants
    }
}