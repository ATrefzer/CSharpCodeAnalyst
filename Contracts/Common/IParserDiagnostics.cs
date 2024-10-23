namespace Contracts.Common;

public interface IParserDiagnostics
{
    string FormatFailures();
    string FormatWarnings();
}