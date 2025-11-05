using CodeGraph.Graph;

namespace CSharpCodeAnalyst.Help;

public class QuickInfo
{
    public QuickInfo()
    {
    }

    public QuickInfo(string title)
    {
        Title = title;
    }

    public string Title { get; set; } = string.Empty;
    public List<ContextInfoLine> Lines { get; set; } = [];
    public List<SourceLocation> SourceLocations { get; set; } = [];
}

public class ContextInfoLine
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}