using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Shared.Services;

public enum EditorType
{
    Notepad,
    NotepadPlusPlus,
    VisualStudio,
    VsCode
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
    ///     Hierarchy of preferred editors to try: Visual Studio (newest first), VS Code,
    ///     Notepad++, plain Notepad as the last resort.
    /// </summary>
    private static readonly List<(EditorType Type, string Path)> KnownEditors = BuildKnownEditors();

    /// <summary>
    ///     User-configured editor (Settings → User Preferences → Preferred Editor). Null means
    ///     auto-detect: fall back to <see cref="KnownEditors" />'s built-in order. Set once at
    ///     startup from <c>UserPreferences.PreferredEditor</c> and again whenever the settings
    ///     dialog is saved.
    /// </summary>
    public static EditorType? PreferredEditor { get; set; }

    private static List<(EditorType, string)> BuildKnownEditors()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var editors = new List<(EditorType, string)>();

        // Visual Studio, newest first. VS 2026 installs under the major version ("18"), older versions under the year.
        string[] vsEditions = ["Enterprise", "Professional", "Community"];
        foreach (var version in new[] { "18", "2022" })
        {
            foreach (var edition in vsEditions)
            {
                editors.Add((EditorType.VisualStudio,
                    Path.Combine(programFiles, "Microsoft Visual Studio", version, edition, @"Common7\IDE\devenv.exe")));
            }
        }

        // VS 2019 and older are 32-bit and live under Program Files (x86).
        foreach (var edition in vsEditions)
        {
            editors.Add((EditorType.VisualStudio,
                Path.Combine(programFilesX86, "Microsoft Visual Studio", "2019", edition, @"Common7\IDE\devenv.exe")));
        }

        // VS Code: the default per-user install (what the Windows installer offers by default)
        // goes under %LocalAppData%\Programs; a system-wide install goes under Program Files.
        editors.Add((EditorType.VsCode, Path.Combine(localAppData, @"Programs\Microsoft VS Code\Code.exe")));
        editors.Add((EditorType.VsCode, Path.Combine(programFiles, @"Microsoft VS Code\Code.exe")));

        editors.Add((EditorType.NotepadPlusPlus, Path.Combine(programFiles, @"Notepad++\notepad++.exe")));
        editors.Add((EditorType.Notepad,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "notepad.exe")));

        return editors;
    }

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
        var (editorType, editorPath) = SelectEditor();

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

        // Reusing a running Visual Studio instance is only appropriate when VS is the (auto-detected
        // or explicitly preferred) editor - not when the user picked something else.
        if (editorType == EditorType.VisualStudio && OpenFileInRunningVisualStudioInstance(filePath, line))
        {
            // If we can open in running VS instance, we are done
            return;
        }

        if (!File.Exists(editorPath))
        {
            throw new FileNotFoundException($"Editor executable not found: {editorPath}", editorPath);
        }

        // Default only open file
        var args = filePath;

        switch (editorType)
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

            case EditorType.VsCode:
                // "--goto file:line:column" reuses an already-running window automatically (VS
                // Code is single-instance by default) regardless of which folder/workspace it has
                // open - no project-dir gymnastics needed, unlike Rider's CLI.
                args = $"--goto \"{filePath}:{line}:{column}\"";
                break;
        }

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = editorPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = false,
            CreateNoWindow = true
        };
        process.Start();
    }

    /// <summary>
    ///     Picks the editor to launch: the user's <see cref="PreferredEditor" /> when it is set and
    ///     an installation of it was found, otherwise the first installed editor from
    ///     <see cref="KnownEditors" />'s built-in (auto-detect) order.
    /// </summary>
    private static (EditorType Type, string Path) SelectEditor()
    {
        if (PreferredEditor is { } preferred)
        {
            var match = KnownEditors.FirstOrDefault(h => h.Type == preferred && File.Exists(h.Path));
            if (match.Path is not null)
            {
                return match;
            }
        }

        return KnownEditors.First(h => File.Exists(h.Path));
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
        // Newest first: 18.0 (VS 2026), 17.0 (VS 2022), 16.0 (VS 2019).
        string[] progIds = ["VisualStudio.DTE.18.0", "VisualStudio.DTE.17.0", "VisualStudio.DTE.16.0"];

        object? obj = null;
        object? mainWindow = null;
        object? itemOperations = null;

        try
        {
            obj = progIds.Select(GetComObject).FirstOrDefault(o => o is not null);
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