namespace Regression.SpecificBugs.MemberAccessDuplicate;

public class SearchGraphSource
{
    public SearchNode OriginalElement { get; set; }
}

public class SearchNode
{
    public string Name { get; set; }
}

public class MemberAccessDuplicate
{
    public void TestMethod()
    {
        var searchGraphSource = new SearchGraphSource();

        // This line should create duplicate SourceLocations for OriginalElement:
        // - One from MemberAccessExpressionSyntax (searchGraphSource.OriginalElement)
        // - One from IdentifierNameSyntax (OriginalElement)
        var proxySource = searchGraphSource.OriginalElement;

        // Another test case
        var elementName = searchGraphSource.OriginalElement.Name;
    }
}