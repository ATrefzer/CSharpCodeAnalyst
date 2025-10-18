namespace Contracts.Common;

public interface IParserDiagnostics
{

    List<string> Failures { get; }
    List<string> Warnings { get; }
    bool HasDiagnostics
    {
        get => Failures.Any() || Warnings.Any();
    }

    string FormatFailures();
    string FormatWarnings();
}