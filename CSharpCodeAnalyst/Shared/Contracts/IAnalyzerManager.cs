namespace CSharpCodeAnalyst.Shared.Contracts;

public interface IAnalyzerManager
{
    IAnalyzer GetAnalyzer(string id);

    IEnumerable<IAnalyzer> All { get; }
}