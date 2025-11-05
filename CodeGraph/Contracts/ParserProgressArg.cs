namespace CodeGraph.Contracts;

public class ParserProgressArg(string message) : EventArgs
{
    public string Message { get; } = message;
}