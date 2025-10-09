namespace Contracts.Graph;

public static class Traversal
{
    public static void Dfs(CodeElement element, Action<CodeElement> handler)
    {
        HashSet<string> visited =
        [
            element.Id
        ];

        foreach (var child in element.Children)
        {
            if (!visited.Contains(child.Id))
            {
                Dfs(child, visited, handler);
            }
        }

        handler(element);
    }
    
    
    public static void Dfs(CodeElement element, HashSet<string> visited, Action<CodeElement> handler)
    {
        visited.Add(element.Id);

        foreach (var child in element.Children)
        {
            if (!visited.Contains(child.Id))
            {
                Dfs(child, visited, handler);
            }
        }

        handler(element);
    }
}