namespace Contracts.Common;

public interface IParserDiagnostics
{
    string FormatFailures();
    string FormatWarnings();
    
    List<string> Failures { get; }
    List<string> Warnings { get; }
}