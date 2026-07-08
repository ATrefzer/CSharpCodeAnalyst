using CSharpCodeAnalyst.CodeGraph.Contracts;

namespace CSharpCodeAnalyst.CodeParser.Parser;

public class Progress : IProgress
{
    public event EventHandler<ParserProgressArg>? ParserProgress;

    public void SendProgress(string message)
    {
        ParserProgress?.Invoke(this, new ParserProgressArg(message));
    }
}