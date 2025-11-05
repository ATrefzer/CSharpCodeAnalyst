namespace CodeGraph.Graph;

public class SourceLocation
{
    public SourceLocation()
    {
    }

    public SourceLocation(string file, int line, int column)
    {
        File = file;
        Line = line;
        Column = column;
    }

    public string? File { get; init; }
    public int Line { get; init; }

    public int Column { get; init; }

    public override string ToString()
    {
        return $"{File}:{Line},{Column}";
    }

    public override bool Equals(object? obj)
    {
        if (obj is not SourceLocation other)
        {
            return false;
        }

        return other.File == File && other.Line == Line && other.Column == Column;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(File, Line, Column);
    }
}