using CSharpCodeAnalyst.TreeMap.Interfaces;

namespace CSharpCodeAnalyst.TreeMap.Tools
{
    public interface IHighlighting
    {
        bool IsHighlighted(IHierarchicalData data);
    }
}