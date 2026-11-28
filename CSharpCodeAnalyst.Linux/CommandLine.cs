namespace CSharpCodeAnalyst.Linux;

/// <summary>
///     Entry point of the Linux command line, separated from Program so that the tests
///     can run it with their own writers.
/// </summary>
internal static class CommandLine
{
    /// <summary>The command ran and wrote its output.</summary>
    public const int Success = 0;

    /// <summary>
    ///     The command line was wrong or the command failed, see the error output. Same value as
    ///     "validation failed" in the application's own command line.
    /// </summary>
    public const int Failure = 2;

    public const string Usage = """
                                CSharpCodeAnalyst.Linux - code graphs where the application cannot run

                                Imports a C++, Python or Java project with doxygen and writes the code graph
                                in the plain text graph format. Open the file on Windows with
                                Import > Plain text in CSharpCodeAnalyst.

                                Usage:
                                  CSharpCodeAnalyst.Linux -doxygen -source:<dir> -out:<file> [options]

                                Options:
                                  -source:<dir>            Source directory doxygen runs over (recursively). Required.
                                  -out:<file>              Graph file to write. Required.
                                  -language:<language>     cpp (default), python or java.
                                  -hierarchy:<mode>        declared (default): namespaces / packages from the code.
                                                           directories: namespaces from the directory structure.
                                  -name:<name>             Project name, the root of the graph. Default: the source
                                                           directory's name.
                                  -work-dir:<dir>          Keep the Doxyfile and doxygen's XML in this directory.
                                                           Default: a temporary directory that is deleted afterwards.
                                  -help                    Show this text.

                                doxygen must be on the PATH (Ubuntu: sudo apt install doxygen).

                                Exit codes: 0 = graph written, 2 = failed (see error output).
                                """;

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        Arguments arguments;
        try
        {
            arguments = Arguments.Parse(args);
        }
        catch (ArgumentException e)
        {
            return Fail(error, e.Message);
        }

        if (args.Length == 0 || arguments.Has("help") || arguments.Has("?"))
        {
            output.WriteLine(Usage);
            return args.Length == 0 ? Failure : Success;
        }

        if (!arguments.Has("doxygen"))
        {
            return Fail(error, "No command given. The only command is -doxygen.");
        }

        if (!DoxygenExportOptions.TryParse(arguments, out var options, out var problem))
        {
            return Fail(error, problem);
        }

        try
        {
            await new DoxygenExportCommand(options, output).ExecuteAsync(cancellationToken);
            return Success;
        }
        catch (OperationCanceledException)
        {
            error.WriteLine("Cancelled.");
            return Failure;
        }
        catch (Exception e)
        {
            // Every failure the run can hit (doxygen missing or failing, unreadable XML, an output
            // path we cannot write) ends up here. The message is meant for a person reading a
            // terminal; the stack trace would only bury it.
            error.WriteLine($"Error: {e.Message}");
            return Failure;
        }
    }

    private static int Fail(TextWriter error, string message)
    {
        error.WriteLine($"Error: {message}");
        error.WriteLine("Run with -help for the usage.");
        return Failure;
    }
}
