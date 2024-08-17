namespace CodeParser.Analysis.Cycles;

[Serializable]
internal class IncompleteLogicException : Exception
{
    public IncompleteLogicException()
    {
    }

    public IncompleteLogicException(string? message) : base(message)
    {
    }

    public IncompleteLogicException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}