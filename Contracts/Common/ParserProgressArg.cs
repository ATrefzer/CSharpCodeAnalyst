namespace Contracts.Common;

public class ParserProgressArg(string message) : EventArgs
{
    public string Message { get; set; } = message;
}