using Contracts.Common;
using Microsoft.CodeAnalysis;

namespace CodeParser.Parser;

internal class ParserDiagnostics : IParserDiagnostics
{
    public List<WorkspaceDiagnostic> Diagnostics { get; } = [];

    public string FormatFailures()
    {
        var failures = Diagnostics.Where(d => d.Kind == WorkspaceDiagnosticKind.Failure).Select(d => d.Message);
        return string.Join(Environment.NewLine, failures);
    }

    public string FormatWarnings()
    {
        var failures = Diagnostics.Where(d => d.Kind == WorkspaceDiagnosticKind.Warning).Select(d => d.Message);
        return string.Join(Environment.NewLine, failures);
    }

    public void Add(WorkspaceDiagnostic diagnostic)
    {
        Diagnostics.Add(diagnostic);
    }

    public void Clear()
    {
        Diagnostics.Clear();
    }
}