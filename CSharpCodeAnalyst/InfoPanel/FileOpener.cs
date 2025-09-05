using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

// ReSharper disable IdentifierTypo

namespace CSharpCodeAnalyst.InfoPanel;

public enum EditorType
{
    Notepad,
    NotepadPlusPlus,
    VisualStudio
}

/// <summary>
///     Tries to open a given text file and jumps (if possible) to line and column.
///     Not every editor supports this.
/// </summary>
public class FileOpener
{
    /// <summary>
    ///     Hierarchy of preferred editors to try
    /// </summary>
    private static readonly List<(EditorType, string)> KnownEditors =
    [
        (EditorType.VisualStudio, @"C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\devenv.exe"),
        (EditorType.VisualStudio, @"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe"),
        (EditorType.NotepadPlusPlus, @"C:\Program Files\Notepad++\notepad++.exe"),
        (EditorType.Notepad, @"C:\Windows\notepad.exe")
    ];

    private readonly EditorType _editor;

    private readonly string _editorPath;

    public FileOpener()
    {
        var editor = KnownEditors.First(h => File.Exists(h.Item2));
        _editor = editor.Item1;
        _editorPath = editor.Item2;
    }

    public void TryOpenFile(string? filePath, int line, int column)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            // Nothing to open
            return;
        }

        if (!File.Exists(filePath))
        {
            // A file was provided but it does not exist
            throw new FileNotFoundException($"File to open not found: {filePath}", filePath);
        }

        if (OpenFileInRunningVisualStudioInstance(filePath, line))
        {
            // If we can open in running VS instance, we are done
            return;
        }

        if (!File.Exists(_editorPath))
        {
            throw new FileNotFoundException($"Editor executable not found: {_editorPath}", _editorPath);
        }

        // Default only open file
        var args = filePath;

        switch (_editor)
        {
            case EditorType.Notepad:
                // Notepad does not support line/column arguments
                args = $"\"{filePath}\"";
                break;

            case EditorType.NotepadPlusPlus:
                args = $"-n{line} -c{column} \"{filePath}\"";
                break;

            case EditorType.VisualStudio:
                // Note: Jumping to a line is not possible if a Visual Studio instance is already running.
                args = $"/Edit \"{filePath}\" /Command \"Edit.Goto {line}\"";
                break;
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _editorPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = false,
                CreateNoWindow = true
            }
        };
        process.Start();
    }

    public static object? GetComObject(string progId)
    {
        var hr = CLSIDFromProgID(progId, out var clsid);
        if (hr != 0)
        {
            return null;
        }

        ComInterop.GetActiveObject(ref clsid, IntPtr.Zero, out var obj);
        return obj;
    }

    [DllImport("ole32.dll", CharSet = CharSet.Unicode)]
    private static extern int CLSIDFromProgID([MarshalAs(UnmanagedType.LPWStr)] string progId, out Guid clsid);

    public static bool OpenFileInRunningVisualStudioInstance(string file, int line = 0)
    {
        // "VisualStudio.DTE.17.0" (VS 2022),
        // "VisualStudio.DTE.16.0" (VS 2019)

        var progId = "VisualStudio.DTE.17.0";

        var obj = GetComObject(progId);
        if (obj is null)
        {
            // No running instance found
            return false;
        }

        dynamic dte = obj;
        dte.MainWindow.Visible = true;
        dte.ItemOperations.OpenFile(file);

        if (line > 0)
        {
            var sel = dte.ActiveDocument.Selection;
            sel.GotoLine(line, true);
        }

        dte.MainWindow.Activate();
        return true;
    }

    public static class ComInterop
    {
        [DllImport("oleaut32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        public static extern void GetActiveObject(ref Guid rclsid, IntPtr reserved,
            [MarshalAs(UnmanagedType.Interface)] out object ppunk);
    }
}