namespace CSharpCodeAnalyst.CodeGraph.Contracts;

public interface IProgress
{
    event EventHandler<ParserProgressArg> ParserProgress;
}