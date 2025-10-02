using System.Runtime.InteropServices;

namespace CSharpCodeAnalyst.CommandLine;

public static class ConsoleHelper
{
    private const int AttachParentProcess = -1;

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    public static void EnsureConsole()
    {
        // Try to attach to an existing console
        if (!AttachConsole(AttachParentProcess))
        {
            // Only allocate a new console if attaching fails
            AllocConsole();
        }
    }
}