using System.Diagnostics;

namespace CSharpCodeAnalyst.Importers.Shared;

/// <summary>
///     Runs an external tool and waits for it. Shared by every importer that shells out - doxygen
///     and the Dart extractor - so the three things that are easy to get wrong are solved once:
///     both output streams are drained while waiting (a child that fills a redirected pipe blocks
///     forever otherwise), arguments are quoted by the framework, and a cancelled run actually
///     kills the child.
/// </summary>
internal static class ProcessRunner
{
    /// <summary>
    ///     Every await is configured not to resume on the caller's context, so that
    ///     <see cref="IsAvailable" /> can block on this without deadlocking the UI thread.
    /// </summary>
    public static async Task<Result> RunAsync(Options options, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo(options.FileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        if (options.WorkingDirectory is not null)
        {
            startInfo.WorkingDirectory = options.WorkingDirectory;
        }

        // ArgumentList quotes each argument for us - paths with spaces are common here.
        foreach (var argument in options.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
                            ?? throw new InvalidOperationException($"Failed to start '{options.FileName}'.");

        // Deliberately not cancellable: after a kill the pipes close and both reads complete on
        // their own. Cancelling them instead would leave two faulted tasks nobody observes.
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Merely giving up the wait would leave the child running: disposing the Process only
            // releases our handle, and the tool would keep writing into pipes nobody reads.
            TryKill(process);
            throw;
        }

        return new Result(process.ExitCode, await standardOutput.ConfigureAwait(false), await standardError.ConfigureAwait(false));
    }

    /// <summary>
    ///     Whether the tool can be started at all and answers with a success exit code - the usual
    ///     way to probe a prerequisite before offering an import. A tool that does not answer within
    ///     <paramref name="timeout" /> counts as unavailable rather than blocking the caller forever.
    /// </summary>
    public static bool IsAvailable(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        try
        {
            using var timeoutSource = new CancellationTokenSource(timeout);
            var result = RunAsync(new Options(fileName, arguments), timeoutSource.Token).GetAwaiter().GetResult();
            return result.ExitCode == 0;
        }
        catch (Exception e) when (e is OperationCanceledException or InvalidOperationException or SystemException)
        {
            // Not on the PATH, not executable, or hung - all of them mean "cannot be used".
            return false;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(true);
        }
        catch (Exception e) when (e is InvalidOperationException or SystemException)
        {
            // It exited by itself in the meantime, or we are not allowed to - nothing to do.
        }
    }

    internal sealed record Options(string FileName, IReadOnlyList<string> Arguments, string? WorkingDirectory = null);

    internal sealed record Result(int ExitCode, string StandardOutput, string StandardError)
    {
        /// <summary>
        ///     The last few lines of whichever stream carries the diagnosis, for an error message.
        /// </summary>
        public string ErrorTail
        {
            get
            {
                var text = StandardError.Trim().Length > 0 ? StandardError : StandardOutput;
                text = text.Trim();
                const int maxLength = 500;
                return text.Length <= maxLength ? text : "..." + text[^maxLength..];
            }
        }
    }
}
