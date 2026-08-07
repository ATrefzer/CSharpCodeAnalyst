// ============================================================================
// PIPELINE COMMENTARY 2.0 - Parallel dotnet + pipeline runner
// ============================================================================

using System.Diagnostics;

namespace PipelineDocsCli;

internal static class AlongsideRunner
{
    public static async Task<int> RunAsync(
        string dotnetVerb,
        IReadOnlyList<string> dotnetArguments,
        Func<CancellationToken, Task> pipelineWork,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            }
        };

        process.StartInfo.ArgumentList.Add(dotnetVerb);
        foreach (var argument in dotnetArguments) process.StartInfo.ArgumentList.Add(argument);

        Console.WriteLine($"▶ Starting dotnet {dotnetVerb} and Pipeline 2.0 analysis side by side.");
        if (!process.Start()) throw new InvalidOperationException("dotnet process could not be started.");

        var analysisTask = Task.Run(async () =>
        {
            try
            {
                await pipelineWork(cancellationToken);
                Console.WriteLine("✓ Pipeline 2.0 snapshot refreshed.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Console.WriteLine("• Pipeline 2.0 analysis cancelled.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"⚠ Pipeline 2.0 analysis failed without blocking dotnet {dotnetVerb}: {ex.Message}");
            }
        }, cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        await analysisTask;
        return process.ExitCode;
    }
}
