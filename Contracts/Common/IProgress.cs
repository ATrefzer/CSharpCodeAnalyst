namespace Contracts.Common;

public interface IProgress
{
    event EventHandler<ParserProgressArg> ParserProgress;
}