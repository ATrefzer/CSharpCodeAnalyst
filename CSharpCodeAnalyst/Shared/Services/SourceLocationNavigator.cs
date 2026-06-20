using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using CodeGraph.Graph;
using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Shared.Services;

public enum EditorType
{
    Notepad,
    NotepadPlusPlus,
    VisualStudio
}

/// <summary>
///     "Jump to code": opens the single source location of a code element or relationship in
///     the editor. It only applies when there is <em>exactly one</em> location.
///     A relationship (or bundled edge) can map to several locations; those are left to the
///     Info panel, which lists all of them as links.
/// </summary>
public static class SourceLocationNavigator
{

    /// <summary>
    ///     Hierarchy of preferred editors to try
    /// </summary>
    private static readonly List<(EditorType, string)> KnownEditors =
    [
        (EditorType.VisualStudio, @"C:\Program Files\Microsoft Visual Studio\18\Professional\Common7\IDE\devenv.exe"),
        (EditorType.VisualStudio, @"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\devenv.exe"),
        (EditorType.VisualStudio, @"C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\devenv.exe"),
        (EditorType.VisualStudio, @"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe"),
        (EditorType.NotepadPlusPlus, @"C:\Program Files\Notepad++\notepad++.exe"),
        (EditorType.Notepad, @"C:\Windows\notepad.exe")
    ];

    /// <summary>A code element can be jumped to if it is not a namespace and has one location.</summary>
    public static bool CanJump(CodeElement? element)
    {
        return element is { ElementType: not CodeElementType.Namespace and not CodeElementType.Assembly, SourceLocations.Count: 1 };
    }

    /// <summary>
    ///     An edge can be jumped to only when it is a single relationship with a single location
    ///     (so bundled edges, and relationships with several call sites, are excluded).
    /// </summary>
    public static bool CanJump(IReadOnlyList<Relationship> relationships)
    {
        return relationships is [{ SourceLocations.Count: 1 }];
    }

    public static void JumpTo(CodeElement element)
    {
        if (CanJump(element))
        {
            Open(element.SourceLocations[0]);
        }
    }

    public static void JumpTo(IReadOnlyList<Relationship> relationships)
    {
        if (CanJump(relationships))
        {
            Open(relationships[0].SourceLocations[0]);
        }
    }

    public static void Open(SourceLocation location)
    {
        try
        {
            Open(location.File, location.Line, location.Column);
        }
        catch (Exception ex)
        {
            var message = string.Format(Strings.OperationFailed_Message, ex.Message);
            MessageBox.Show(message, Strings.Error_Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public static void Open(string? filePath, int line, int column)
    {
        var editor = KnownEditors.First(h => File.Exists(h.Item2));
        var _editor = editor.Item1;
        var _editorPath = editor.Item2;

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

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = _editorPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = false,
            CreateNoWindow = true
        };
        process.Start();
    }

    private static object? GetComObject(string progId)
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

    private static bool OpenFileInRunningVisualStudioInstance(string file, int line = 0)
    {
        // "VisualStudio.DTE.17.0" (VS 2022),
        // "VisualStudio.DTE.16.0" (VS 2019)
        var progId = "VisualStudio.DTE.18.0";

        object? obj = null;
        object? mainWindow = null;
        object? itemOperations = null;

        try
        {
            obj = GetComObject(progId);
            if (obj is null)
            {
                return false;
            }

            dynamic dte = obj;

            mainWindow = dte.MainWindow;
            ((dynamic)mainWindow).Visible = true;

            itemOperations = dte.ItemOperations;
            ((dynamic)itemOperations).OpenFile(file);

            if (line > 0)
            {
                // Unfortunately if visual studio is busy we may fail here with RPC_E_CALL_REJECTED
                // A retry solves this in most cases.
                WithRetry(3, () => GotoLineInActiveDocument(dte, line));
            }

            ((dynamic)mainWindow).Activate();

            return true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex);
            return false;
        }
        finally
        {
            if (itemOperations != null)
            {
                Marshal.ReleaseComObject(itemOperations);
            }

            if (mainWindow != null)
            {
                Marshal.ReleaseComObject(mainWindow);
            }

            if (obj != null)
            {
                Marshal.ReleaseComObject(obj);
            }
        }
    }


    private static void WithRetry(int number, Action action)
    {
        var counter = 0;
        while (counter < number)
        {
            try
            {
                action();
                return;
            }
            catch (Exception)
            {
                counter++;
                Thread.Sleep(100);
            }
        }

        throw new InvalidOperationException("Retry exceeded");
    }

    private static void GotoLineInActiveDocument(dynamic dte, int line)
    {
        object? activeDoc = null;
        object? selection = null;

        try
        {
            activeDoc = dte.ActiveDocument;
            if (activeDoc != null)
            {
                selection = ((dynamic)activeDoc).Selection;
                if (selection != null)
                {
                    ((dynamic)selection).GotoLine(line, true);
                }
            }
        }
        finally
        {
            if (selection != null)
            {
                Marshal.ReleaseComObject(selection);
            }

            if (activeDoc != null)
            {
                Marshal.ReleaseComObject(activeDoc);
            }
        }
    }


    private static class ComInterop
    {
        [DllImport("oleaut32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        public static extern void GetActiveObject(ref Guid rclsid, IntPtr reserved,
            [MarshalAs(UnmanagedType.Interface)] out object ppunk);
    }
}