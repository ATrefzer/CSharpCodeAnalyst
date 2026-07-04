using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.AnalyzerSdk.Messages;

public class ShowPartitionsRequest
{

    public ShowPartitionsRequest(CodeElement codeElement, bool includeBaseClasses)
    {
        CodeElement = codeElement;
        IncludeBaseClasses = includeBaseClasses;
    }

    public CodeElement CodeElement { get; }
    public bool IncludeBaseClasses { get; }
}