using CSharpCodeAnalyst.Common.Contracts;

namespace CSharpCodeAnalyst.TreeMap.Tools
{
    public interface IHighlighting
    {
        bool IsHighlighted(IHierarchicalData data);
    }
}