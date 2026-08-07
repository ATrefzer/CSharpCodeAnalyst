// PipelineDocsCli — durable architecture maps + per-file PIPELINE headers
// From the original pipeline-documentation workflow (Phoenix Visualizer tools lineage).

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
                "headers" => RunHeaders(args[1..]),
                // Back-compat: bare --project-dir runs option 2 (per-file headers)
                _ when args.Contains("--project-dir", StringComparer.OrdinalIgnoreCase) => RunHeaders(args),
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

    /// <summary>
    /// Option 2: write/update auto-generated PIPELINE DOCUMENTATION headers at the top of each .cs file.
    /// </summary>
    private static int RunHeaders(string[] args)
    {
        var options = ParseHeaderArguments(args);
        if (string.IsNullOrWhiteSpace(options.ProjectDir))
        {
            Console.Error.WriteLine("headers requires --project-dir <path>.");
            return 2;
        }

        try
        {
            var generator = new PipelineDocsGenerator(options);
            var result = generator.Run();
            Console.WriteLine(
                $"Header pass processed {result.FilesProcessed}; " +
                $"updated {result.FilesUpdated}; skipped {result.FilesSkipped}" +
                (options.DryRun ? " (dry-run)." : "."));
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Header generation failed: {ex.Message}");
            return 1;
        }
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

    private static GeneratorOptions ParseHeaderArguments(string[] args)
    {
        var options = new GeneratorOptions();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--project-dir" when i + 1 < args.Length:
                    options.ProjectDir = args[++i];
                    break;
                case "--files" when i + 1 < args.Length:
                    options.Files = args[++i]
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(f => f.Trim('"'))
                        .ToList();
                    break;
                case "--dry-run":
                    options.DryRun = true;
                    break;
                case "--max-calls" when i + 1 < args.Length && int.TryParse(args[++i], out var maxCalls):
                    options.MaxCalls = maxCalls;
                    break;
                case "--update-existing":
                    options.UpdateExisting = ReadOptionalBool(args, ref i, true);
                    break;
                case "--replace-manual":
                    options.ReplaceManual = ReadOptionalBool(args, ref i, true);
                    break;
                case "--verbose":
                    options.Verbose = true;
                    break;
            }
        }

        return options;
    }

    private static bool ReadOptionalBool(string[] args, ref int index, bool fallback)
    {
        if (index + 1 < args.Length && bool.TryParse(args[index + 1], out var value))
        {
            index++;
            return value;
        }

        return fallback;
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
PipelineDocsCli — document apps for reverse-engineering and rebuild

COMMANDS
  1) snapshot [options]
       Write a solution-native .pipeline map (projects, types, calls, creates…).

  2) headers --project-dir <path> [options]
       Write/update auto PIPELINE DOCUMENTATION headers at the top of each .cs file
       (the original per-file banner workflow).

  alongside build [options] -- [dotnet build arguments]
  alongside run   [options] -- [dotnet run arguments]
       Refresh snapshot beside a build/run (never gates the build).

SNAPSHOT OPTIONS
  --repo <path>          Repository root (default: current directory)
  --solution <path>      .sln path (default: first root-level .sln)
  --output <path>        Output path (default: docs/pipeline/<SolutionName>.pipeline)

HEADER OPTIONS
  --project-dir <path>   Required. Root to scan for .cs files
  --files "a.cs b.cs"    Optional explicit file list
  --dry-run              Analyze and report without writing
  --max-calls <n>        Top method/create names kept in header (default 10)
  --update-existing      Update files that already have auto headers (default true)
  --replace-manual       Also replace non-auto PIPELINE DOCUMENTATION blocks
  --verbose              Log each file

EXAMPLES
  dotnet run --project samples/PipelineDocsCli -- snapshot --solution CSharpCodeAnalyst.sln
  dotnet run --project samples/PipelineDocsCli -- headers --project-dir CSharpCodeAnalyst --dry-run
  dotnet run --project samples/PipelineDocsCli -- headers --project-dir MyApp.Core --verbose
  dotnet run --project samples/PipelineDocsCli -- alongside build -- MyApp.sln

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

internal sealed class GeneratorOptions
{
    public string ProjectDir { get; set; } = "";
    public List<string> Files { get; set; } = new();
    public bool DryRun { get; set; }
    public int MaxCalls { get; set; } = 10;
    public bool UpdateExisting { get; set; } = true;
    public bool ReplaceManual { get; set; }
    public bool Verbose { get; set; }
}

internal sealed class GeneratorResult
{
    public int FilesProcessed { get; set; }
    public int FilesUpdated { get; set; }
    public int FilesSkipped { get; set; }
}
