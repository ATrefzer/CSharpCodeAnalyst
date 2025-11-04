using Contracts.Graph;

namespace CSharpCodeAnalyst.Analyzers.EventRegistration;

public class Result
{
    public Result(CodeElement handler, CodeElement evt, List<SourceLocation> locations)
    {
        Handler = handler;
        Event = evt;
        Locations = locations;
    }

    public CodeElement Handler { get; }
    public CodeElement Event { get; }
    public List<SourceLocation> Locations { get; }
}