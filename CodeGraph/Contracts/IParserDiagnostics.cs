namespace CodeGraph.Contracts;

public interface IParserDiagnostics
{
    List<string> Failures { get; }
    List<string> Warnings { get; }
    bool HasDiagnostics { get; }
    string FormatFailures();
    string FormatWarnings();
}