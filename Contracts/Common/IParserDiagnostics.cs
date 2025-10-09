namespace Contracts.Common;

public interface IParserDiagnostics
{

    List<string> Failures { get; }
    List<string> Warnings { get; }
    string FormatFailures();
    string FormatWarnings();
}