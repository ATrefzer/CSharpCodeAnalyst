using CSharpCodeAnalyst.CodeGraph.Algorithms.Partitioning;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.AnalyzerSdk.Messages;

public class ShowPartitionsRequest
{
    /// <param name="options">
    ///     How the partitions are formed. Pass what the sender measured with, otherwise the view
    ///     groups the members differently than the number the user clicked on.
    /// </param>
    public ShowPartitionsRequest(CodeElement codeElement, PartitionOptions options)
    {
        CodeElement = codeElement;
        Options = options;
    }

    public CodeElement CodeElement { get; }
    public PartitionOptions Options { get; }
}
