// PipelineDocsCli — solution-native .pipeline documentation writer
// Snapshot topology for reverse-engineering / rebuilding real apps.

namespace PipelineDocsCli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
        {
            ShowHelp();
            return 0;
        }

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "snapshot" => await RunSnapshotAsync(args[1..], cancellation.Token),
                "alongside" => await RunAlongsideAsync(args[1..], cancellation.Token),
                _ => UnknownCommand(args[0])
            };
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Pipeline operation cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"PipelineDocsCli failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> RunSnapshotAsync(string[] args, CancellationToken cancellationToken)
    {
        var options = SnapshotOptions.Parse(args);
        var snapshot = await RepositoryPipelineAnalyzer.AnalyzeAsync(
            options.RepositoryRoot, options.SolutionPath, cancellationToken);
        var output = options.ResolveOutputPath(snapshot);
        await PipelineDocumentWriter.WriteAsync(snapshot, output, cancellationToken);
        Console.WriteLine($"Wrote {Path.GetRelativePath(options.RepositoryRoot, output)}");
        Console.WriteLine(
            $"  projects: {snapshot.Projects.Count} " +
            $"(solution {snapshot.Projects.Count(p => p.IsSolutionMember)}, " +
            $"adjacent {snapshot.Projects.Count(p => !p.IsSolutionMember)}); " +
            $"diagnostics: {snapshot.Diagnostics.Count}");
        return 0;
    }

    private static async Task<int> RunAlongsideAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 0 || args[0] is not ("build" or "run"))
        {
            Console.Error.WriteLine("alongside requires either 'build' or 'run'.");
            return 2;
        }

        var verb = args[0];
        var separator = Array.IndexOf(args, "--");
        var pipelineArgs = separator >= 0 ? args[1..separator] : args[1..];
        var dotnetArgs = separator >= 0 ? args[(separator + 1)..] : Array.Empty<string>();
        var options = SnapshotOptions.Parse(pipelineArgs);

        return await AlongsideRunner.RunAsync(
            verb,
            dotnetArgs,
            token => GenerateSnapshotAsync(options, token),
            cancellationToken);
    }

    private static async Task GenerateSnapshotAsync(SnapshotOptions options, CancellationToken cancellationToken)
    {
        var snapshot = await RepositoryPipelineAnalyzer.AnalyzeAsync(
            options.RepositoryRoot, options.SolutionPath, cancellationToken);
        var output = options.ResolveOutputPath(snapshot);
        await PipelineDocumentWriter.WriteAsync(snapshot, output, cancellationToken);
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        ShowHelp();
        return 2;
    }

    private static void ShowHelp()
    {
        Console.WriteLine("""
PipelineDocsCli — write durable architecture maps for reverse-engineering and rebuild

WHY
  Most analysis tools show a live graph. This tool writes a stable, human-readable
  .pipeline document of what Roslyn + MSBuild actually see: projects, refs, types,
  calls, creates, events, I/O. Use it to document apps you (or anyone) built so you
  can debug → reverse-engineer → rebuild with evidence instead of guessing and
  instead of trust-me AI scaffolding with no map.

COMMANDS
  snapshot [options]
      Emit a .pipeline file for a solution + adjacent projects.

  alongside build [options] -- [dotnet build arguments]
  alongside run   [options] -- [dotnet run arguments]
      Refresh the snapshot beside a build/run. Analysis failure never replaces
      the build/run exit code.

OPTIONS
  --repo <path>          Repository root (default: current directory)
  --solution <path>      .sln path (default: first root-level .sln)
  --output <path>        Output path (default: docs/pipeline/<SolutionName>.pipeline)

EXAMPLES
  dotnet run --project samples/PipelineDocsCli -- snapshot --solution CSharpCodeAnalyst.sln
  dotnet run --project samples/PipelineDocsCli -- snapshot --output docs/pipeline/MyApp.pipeline
  dotnet run --project samples/PipelineDocsCli -- alongside build -- MyApp.sln

OPTIONAL PROJECT ANNOTATIONS (MSBuild properties)
  <PipelineRole>occasional-tool</PipelineRole>
  <PipelinePurpose>Why this adjacent project exists.</PipelinePurpose>

See Documentation/pipeline-documentation.md
""");
    }
}

internal sealed record SnapshotOptions(string RepositoryRoot, string? SolutionPath, string? OutputPath)
{
    public static SnapshotOptions Parse(string[] args)
    {
        var repo = Directory.GetCurrentDirectory();
        string? solution = null;
        string? output = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--repo" when i + 1 < args.Length:
                    repo = args[++i];
                    break;
                case "--solution" when i + 1 < args.Length:
                    solution = args[++i];
                    break;
                case "--output" when i + 1 < args.Length:
                    output = args[++i];
                    break;
            }
        }

        return new SnapshotOptions(Path.GetFullPath(repo), solution, output);
    }

    public string ResolveOutputPath(PipelineSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(OutputPath))
        {
            return Path.GetFullPath(
                Path.IsPathRooted(OutputPath) ? OutputPath : Path.Combine(RepositoryRoot, OutputPath));
        }

        var name = Path.GetFileNameWithoutExtension(snapshot.SolutionPath);
        if (string.IsNullOrWhiteSpace(name))
            name = Path.GetFileName(RepositoryRoot);
        return Path.Combine(RepositoryRoot, "docs", "pipeline", $"{name}.pipeline");
    }
}
