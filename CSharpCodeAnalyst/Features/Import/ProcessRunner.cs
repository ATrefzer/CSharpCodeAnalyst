using System.Diagnostics;

namespace CSharpCodeAnalyst.Features.Import;

/// <summary>
///     Runs an external tool and waits for it, draining both output streams while waiting -
///     a child that fills a redirected pipe blocks forever otherwise.
/// </summary>
internal static class ProcessRunner
{
    public static async Task<Result> RunAsync(Options options, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo(options.FileName)
        {
            WorkingDirectory = options.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        // ArgumentList quotes each argument for us - paths with spaces are common here.
        foreach (var argument in options.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
                            ?? throw new InvalidOperationException($"Failed to start '{options.FileName}'.");

        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return new Result(process.ExitCode, await standardOutput, await standardError);
    }

    internal sealed record Options(string FileName, IReadOnlyList<string> Arguments, string WorkingDirectory);

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
