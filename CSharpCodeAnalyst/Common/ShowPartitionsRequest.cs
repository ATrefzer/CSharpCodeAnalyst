using CodeParser.Analysis.Shared;
using Contracts.Graph;

namespace CSharpCodeAnalyst.Common;

public class ShowPartitionsRequest
{
    public CodeElement CodeElement { get; }

    public ShowPartitionsRequest(CodeElement codeElement)
    {
        CodeElement = codeElement;
    }

}