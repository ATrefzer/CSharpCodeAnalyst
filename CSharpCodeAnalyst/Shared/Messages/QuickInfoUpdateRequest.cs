using CSharpCodeAnalyst.Help;

namespace CSharpCodeAnalyst.Messages;

public class QuickInfoUpdateRequest(List<QuickInfo> quickInfo)
{
    public List<QuickInfo> QuickInfo { get; } = quickInfo;
}