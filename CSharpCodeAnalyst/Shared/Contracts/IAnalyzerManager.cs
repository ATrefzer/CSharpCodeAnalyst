namespace CSharpCodeAnalyst.Shared.Contracts;

public interface IAnalyzerManager
{

    IEnumerable<IAnalyzer> All { get; }
    IAnalyzer GetAnalyzer(string id);

    /// <summary>
    /// Collects persistent data from all analyzers
    /// </summary>
    Dictionary<string, string> CollectAnalyzerData();

    /// <summary>
    /// Restores persistent data to all analyzers
    /// </summary>
    void RestoreAnalyzerData(Dictionary<string, string> data);
}