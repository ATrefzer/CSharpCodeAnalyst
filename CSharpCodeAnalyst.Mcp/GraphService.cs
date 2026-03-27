using CodeGraph.Export;

namespace CSharpCodeAnalyst.Mcp;

/// <summary>
/// Holds the loaded CodeGraph and provides access to it.
/// The graph file path is passed via the GRAPH_FILE environment variable.
/// </summary>
public class GraphService
{
    private CodeGraph.Graph.CodeGraph? _graph;
    private string? _loadedFilePath;

    public CodeGraph.Graph.CodeGraph Graph
    {
        get
        {
            if (_graph is null)
            {
                throw new InvalidOperationException(
                    "No graph loaded. Call load_graph first or set GRAPH_FILE environment variable.");
            }
            return _graph;
        }
    }

    public bool IsLoaded => _graph is not null;
    public string? LoadedFilePath => _loadedFilePath;

    public void Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Graph file not found: {filePath}");
        }

        _graph = CodeGraphSerializer.DeserializeFromFile(filePath);
        _loadedFilePath = filePath;
    }
}
