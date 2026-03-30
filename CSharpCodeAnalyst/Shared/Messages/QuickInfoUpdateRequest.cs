using CSharpCodeAnalyst.Features.Help;

namespace CSharpCodeAnalyst.Shared.Messages;

public class QuickInfoUpdateRequest(List<QuickInfo> quickInfo)
{
    public List<QuickInfo> QuickInfo { get; } = quickInfo;
}