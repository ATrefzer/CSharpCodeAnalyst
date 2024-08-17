using CSharpCodeAnalyst.Help;

namespace CSharpCodeAnalyst.Common;

public class QuickInfoUpdate(List<QuickInfo> quickInfo)
{
    public List<QuickInfo> QuickInfo { get; } = quickInfo;
}