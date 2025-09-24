using CSharpCodeAnalyst.Help;

namespace CSharpCodeAnalyst.Messages;

public class QuickInfoUpdate(List<QuickInfo> quickInfo)
{
    public List<QuickInfo> QuickInfo { get; } = quickInfo;
}