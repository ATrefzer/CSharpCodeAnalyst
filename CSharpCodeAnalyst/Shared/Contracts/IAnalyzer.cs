using Contracts.Graph;

namespace CSharpCodeAnalyst.Shared.Contracts;

public interface IAnalyzer
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    void Analyze(CodeGraph graph);

    /// <summary>
    /// Returns persistent data as JSON string, or null if no data to persist
    /// </summary>
    string? GetPersistentData();

    /// <summary>
    /// Sets persistent data from JSON string
    /// </summary>
    void SetPersistentData(string? data);
}