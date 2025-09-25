namespace CSharpCodeAnalyst.Shared.Contracts;

public interface IAnalyzerManager
{

    IEnumerable<IAnalyzer> All { get; }
    IAnalyzer GetAnalyzer(string id);
}