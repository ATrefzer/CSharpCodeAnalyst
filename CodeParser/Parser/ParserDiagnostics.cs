using Contracts.Common;
using Microsoft.CodeAnalysis;

namespace CodeParser.Parser;

internal class ParserDiagnostics : IParserDiagnostics
{
    private List<WorkspaceDiagnostic> Diagnostics { get; } = [];

    public string FormatFailures()
    {
        return string.Join(Environment.NewLine, Failures);
    }

    public string FormatWarnings()
    {
        return string.Join(Environment.NewLine, Failures);
    }

    public List<string> Failures
    {
        get => Diagnostics.Where(d => d.Kind == WorkspaceDiagnosticKind.Failure).Select(d => d.Message).ToList();
    }

    public List<string> Warnings {  get => Diagnostics.Where(d => d.Kind == WorkspaceDiagnosticKind.Warning).Select(d => d.Message).ToList();}

    public void Add(WorkspaceDiagnostic diagnostic)
    {
        Diagnostics.Add(diagnostic);
    }

    public void Clear()
    {
        Diagnostics.Clear();
    }
}