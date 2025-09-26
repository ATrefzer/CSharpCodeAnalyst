using Contracts.Graph;

namespace CSharpCodeAnalyst.Analyzers.ConsistencyRules;

public static class PatternMatcher
{
    /// <summary>
    /// Resolves a pattern like "Business.**" to a set of CodeElement IDs
    /// Pattern format: "Base.Path" or "Base.Path.*" or "Base.Path.**"
    /// </summary>
    public static HashSet<string> ResolvePattern(string pattern, CodeGraph codeGraph)
    {
        var matchingIds = new HashSet<string>();

        if (string.IsNullOrEmpty(pattern))
            return matchingIds;

        // Split pattern into base path and wildcard suffix
        var (basePath, expansionMode) = ParsePattern(pattern);

        // Find the start element by exact FullName match
        var startElement = FindStartElement(basePath, codeGraph);
        if (startElement == null)
            return matchingIds; // No matching start element found

        // Apply expansion based on wildcard suffix
        ApplyExpansion(startElement, expansionMode, matchingIds);

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

    private static CodeElement? FindStartElement(string basePath, CodeGraph codeGraph)
    {
        // Find element with exact FullName match
        return codeGraph.Nodes.Values.FirstOrDefault(element =>
            string.Equals(element.FullName, basePath, StringComparison.OrdinalIgnoreCase));
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
        Self,           // No wildcard - just the element itself
        DirectChildren, // .* - element + direct children
        Recursive       // .** - element + all descendants
    }
}