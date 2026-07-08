namespace CSharpCodeAnalyst.TreeMap.Tools
{
    /// <summary>
    /// Don't ask. Hack to work around the problem that a data template is instantiated only once
    /// in tab control, regardless if we have many view models.
    /// this is a central place where an application can request closing all tool windows.
    /// </summary>
    public class ToolsExtension
    {
        private ToolsExtension()
        {
        }

        public event EventHandler<object> ToolCloseRequested;

        static ToolsExtension()
        {
            _instance = new ToolsExtension();
        }

        private static ToolsExtension _instance;
        public static ToolsExtension Instance => _instance;
        public void CloseToolWindow()
        {
            ToolCloseRequested?.Invoke(this, new EventArgs());
        }
    }
}
