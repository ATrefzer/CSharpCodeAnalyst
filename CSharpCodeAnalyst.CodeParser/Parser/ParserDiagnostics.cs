using CSharpCodeAnalyst.CodeGraph.Contracts;
using Microsoft.CodeAnalysis;

namespace CSharpCodeAnalyst.CodeParser.Parser;

internal class ParserDiagnostics : IParserDiagnostics
{
    private readonly List<string> _failures = [];
    private readonly List<string> _warnings = [];

    public bool HasDiagnostics
    {
        get => _failures.Any() || _warnings.Any();
    }

    public List<string> Failures
    {
        get => _failures;
    }

    public List<string> Warnings
    {
        get => _warnings;
    }

    public string FormatFailures()
    {
        return string.Join(Environment.NewLine, _failures);
    }

    public string FormatWarnings()
    {
        return string.Join(Environment.NewLine, _warnings);
    }

    /// <summary>
    ///     Roslyn workspace diagnostic. The kind is collapsed to the matching bucket here so that our own
    ///     diagnostics (see <see cref="AddWarning" /> / <see cref="AddFailure" />) can share the same lists
    ///     without depending on the non-constructible <see cref="WorkspaceDiagnostic" /> type.
    /// </summary>
    public void Add(WorkspaceDiagnostic diagnostic)
    {
        if (diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
        {
            _failures.Add(diagnostic.Message);
        }
        else
        {
            _warnings.Add(diagnostic.Message);
        }
    }

    public void AddWarning(string message)
    {
        _warnings.Add(message);
    }

    public void AddFailure(string message)
    {
        _failures.Add(message);
    }

    public void Clear()
    {
        _failures.Clear();
        _warnings.Clear();
    }
}
