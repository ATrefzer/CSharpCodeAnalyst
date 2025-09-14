using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Areas.ResultArea;

public class EmptyTableViewModel : TableViewModel
{
    public EmptyTableViewModel()
    {
        Title = Strings.Tab_Summary;
    }

    public override void Clear()
    {
        // Nothing to clear
    }

    public override string ToString()
    {
        return "No data available";
    }
}