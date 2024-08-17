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
            if (edge.UserData is Dependency dependency)
            {
                return CreateEdgeQuickInfos([dependency]);
            }

            if (edge.UserData is List<Dependency> dependencies)
            {
                return CreateEdgeQuickInfos(dependencies);
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

    private List<QuickInfo> CreateEdgeQuickInfos(List<Dependency> dependencies)
    {
        var quickInfos = new List<QuickInfo>();
        foreach (var d in dependencies)
        {
            quickInfos.Add(CreateEdgeQuickInfo(d));
        }

        return quickInfos;
    }

    private QuickInfo CreateEdgeQuickInfo(Dependency dependency)
    {
        var contextInfo = new QuickInfo
        {
            Title = $"Dependency: {dependency.Type.ToString()}",
            Lines =
            [
                new ContextInfoLine { Label = "Source:", Value = GetElementName(dependency.SourceId) },
                new ContextInfoLine { Label = "Target:", Value = GetElementName(dependency.TargetId) }
            ],
            SourceLocations = dependency.SourceLocations
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