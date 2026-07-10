using CSharpCodeAnalyst.Contracts;

namespace CSharpCodeAnalyst.TreeMap.Tools
{
    public interface IHighlighting
    {
        bool IsHighlighted(IHierarchicalData data);
    }
}