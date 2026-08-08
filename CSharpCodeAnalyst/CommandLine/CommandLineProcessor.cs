using System.Diagnostics;
using System.IO;
using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.CommandLine;

public static class CommandLineProcessor
{
    /// <summary>
    ///     0 = No violations found
    ///     1 = Violation found
    ///     2 = Could not perform analysis, see log output.
    /// </summary>
    public static async Task<int> ProcessCommandLine(string[] args)
    {
        try
        {
            var arguments = ParseArguments(args);

            SetupOutput(arguments);

            var cmd = new ConsoleValidationCommand(arguments);
            if (cmd.CanExecute())
            {
                return await cmd.Execute();
            }

            Trace.TraceError(Strings.Cmd_UnknownCommandLineArgs);
            ShowUsage();
            return 2;
        }
        catch (Exception ex)
        {
            Trace.TraceError(ex.ToString());
            return 2;
        }
    }

    /// <summary>
    ///     Note:
    ///     Console.WriteLine gets lost unless a console is attached.
    ///     Redirecting output to a file works in this case.
    ///     With console attached, Console.WriteLine works. But in this case the
    ///     redirect does not work.
    /// </summary>
    private static void SetupOutput(Dictionary<string, string> arguments)
    {
        Trace.Listeners.Clear();

        var logFile = arguments.GetValueOrDefault("log-file");
        var writeToConsole = arguments.ContainsKey("log-console");

        // Trace is written to console
        // Note: > redirect does not work if this is used.
        // Set up before the log file, so that a problem with the log file has somewhere to be
        // reported - with no listener attached at all, the message would be lost.
        if (writeToConsole)
        {
            ConsoleHelper.EnsureConsole();
            var consoleListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleListener);
        }

        // Trace is written to log file
        if (!string.IsNullOrEmpty(logFile))
        {
            try
            {
                EnsureDirectoryExists(logFile);
                var fileListener = new TextWriterTraceListener(logFile);
                //fileListener.TraceOutputOptions = TraceOptions.DateTime;
                Trace.Listeners.Add(fileListener);
            }
            catch (Exception ex)
            {
                // Losing the log file must not abort a validation that would otherwise run. Failing
                // here would also report exit code 2 ("validation failed") for what is really just
                // an unusable path.
                Trace.TraceError(string.Format(Strings.Cmd_LogFileNotWritable, logFile, ex.Message));
            }
        }

        Trace.AutoFlush = true;
    }

    /// <summary>
    ///     Creates the directory of an output file if it does not exist yet.
    /// </summary>
    internal static void EnsureDirectoryExists(string filePath)
    {
        // Null or empty for a bare file name (no directory part), which needs nothing created.
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static Dictionary<string, string> ParseArguments(string[] args)
    {
        var arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var arg in args)
        {
            if (!arg.StartsWith("-"))
            {
                continue;
            }

            if (arg.Contains(":"))
            {
                var colonIndex = arg.IndexOf(":", StringComparison.Ordinal);
                var key = arg[1..colonIndex]; // Remove the "-" prefix
                var value = arg[(colonIndex + 1)..];
                arguments[key] = value;
            }
            else
            {
                var key = arg[1..]; // Remove the "-" prefix
                arguments[key] = "true"; // Flag argument
            }
        }

        return arguments;
    }


    private static void ShowUsage()
    {
        var usage = """

                    --------------------------------------------------------------------------

                    Command line usage:

                    Validate C# solution against architectural rules:

                    CSharpCodeAnalyst.exe   -validate 
                                            -sln:<solution_file> 
                                            -rules:<rules_file> 

                                            [-out:out_file>]
                                            [-sarif:<sarif_file>]
                                            [-source-root:<directory>]
                                            [-log-console]
                                            [-log-file:<log_file>]
                    --------------------------------------------------------------------------

                    """;
        Trace.WriteLine(usage);

        // Example
        // CSharpCodeAnalyst -validate -sln:D:\Repositories\CSharpCodeAnalyst\CSharpCodeAnalyst.sln -rules:d:\rules.txt -log-console -out:d:\analysis-result.txt
    }
}