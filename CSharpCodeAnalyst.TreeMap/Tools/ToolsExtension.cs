namespace CSharpCodeAnalyst.TreeMap.Tools;

/// <summary>
///     Don't ask. Hack to work around the problem that a data template is instantiated only once
///     in tab control, regardless if we have many view models.
///     this is a central place where an application can request closing all tool windows.
/// </summary>
public class ToolsExtension
{

    static ToolsExtension()
    {
        Instance = new ToolsExtension();
    }

    public static ToolsExtension Instance { get; }
    public event EventHandler<object>? ToolCloseRequested;

    public void CloseToolWindow()
    {
        ToolCloseRequested?.Invoke(this, EventArgs.Empty);
    }
}