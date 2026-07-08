using CSharpCodeAnalyst.CodeGraph.Contracts;
using IProgress = CSharpCodeAnalyst.History.Model.IProgress;

namespace CSharpCodeAnalyst.Features.History;

/// <summary>
///     Adapts both  <see cref="CSharpCodeAnalyst.CodeGraph.Contracts.IProgress" /> and
///     <see cref="CSharpCodeAnalyst.History.Model" />.
///     Both mechanisms are forwarded to the MainViewModel, bypassing the ParserProgress event.
///     TODO einheitlich machen
/// </summary>
internal class ProgressAdapter : IProgress, CodeGraph.Contracts.IProgress
{
    private readonly Action<string> _adapter;

    public ProgressAdapter(Action<string> adapter)
    {
        _adapter = adapter;
    }

    public void Message(string msg)
    {
        _adapter(msg);
    }

    public event EventHandler<ParserProgressArg>? ParserProgress;

    public void SendProgress(string msg)
    {
        _adapter(msg);
    }
}