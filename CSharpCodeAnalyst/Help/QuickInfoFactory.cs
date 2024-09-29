using Contracts.Graph;
using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.Help;

public interface IQuickInfoFactory
{
    List<QuickInfo> CrateQuickInfo(object? obj);
}

internal class QuickInfoFactory(CodeGraph graph) : IQuickInfoFactory
{
    internal static readonly List<QuickInfo> DefaultInfo = [new QuickInfo("No object selected")];
    internal static readonly List<QuickInfo> NoInfoProviderRegistered = [new QuickInfo("No info provider registered")];

    public List<QuickInfo> CrateQuickInfo(object? obj)
    {
        if (obj is null)
        {
            return DefaultInfo;
        }

        if (obj is IViewerNode viewerNode)
        {
            var node = viewerNode.Node;
            if (node.UserData is CodeElement codeElement)
            {
                return [CreateNodeQuickInfo(codeElement)];
            }
        }
        else if (obj is IViewerEdge viewerEdge)
        {
            var edge = viewerEdge.Edge;
            if (edge.UserData is Relationship relationship)
            {
                return CreateEdgeQuickInfos([relationship]);
            }

            if (edge.UserData is List<Relationship> relationships)
            {
                return CreateEdgeQuickInfos(relationships);
            }
        }

        return DefaultInfo;
    }

    private QuickInfo CreateNodeQuickInfo(CodeElement codeElement)
    {
        var contextInfo = new QuickInfo
        {
            Title = codeElement.ElementType.ToString(),
            Lines =
            [
                new ContextInfoLine { Label = "Name:", Value = codeElement.Name },
                new ContextInfoLine { Label = "Full name:", Value = GetFullPath(codeElement.Id) }
            ],
            SourceLocations = codeElement.SourceLocations
        };
        return contextInfo;
    }

    private List<QuickInfo> CreateEdgeQuickInfos(List<Relationship> relationships)
    {
        var quickInfos = new List<QuickInfo>();
        foreach (var r in relationships)
        {
            quickInfos.Add(CreateEdgeQuickInfo(r));
        }

        return quickInfos;
    }

    private QuickInfo CreateEdgeQuickInfo(Relationship relationship)
    {
        var contextInfo = new QuickInfo
        {
            Title = $"Relationship: {relationship.Type.ToString()}",
            Lines =
            [
                new ContextInfoLine { Label = "Source:", Value = GetElementName(relationship.SourceId) },
                new ContextInfoLine { Label = "Target:", Value = GetElementName(relationship.TargetId) }
            ],
            SourceLocations = relationship.SourceLocations
        };
        return contextInfo;
    }

    private string GetElementName(string id)
    {
        return graph.Nodes.TryGetValue(id, out var element) ? element.Name : id;
    }

    private string GetFullPath(string id)
    {
        return graph.Nodes.TryGetValue(id, out var element) ? element.FullName : id;
    }
}