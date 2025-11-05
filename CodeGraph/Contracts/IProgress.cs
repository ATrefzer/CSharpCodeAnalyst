namespace CodeGraph.Contracts;

public interface IProgress
{
    event EventHandler<ParserProgressArg> ParserProgress;
}