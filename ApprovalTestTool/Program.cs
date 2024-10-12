using System.Reflection;
using CodeParser.Parser;
using CodeParser.Parser.Config;
using LibGit2Sharp;

namespace ApprovalTestTool;

internal class TestTool
{
    /// <summary>
    /// Automatic approval tool
    /// For each line in the repositories.txt
    ///     1. Clone or pull the repository
    ///     2. Checkout the specified commit
    ///     3. Run test code: Parse the solution and write the output to a file.
    ///     4. Compare output with reference or copy to reference folder if not exists yet.
    ///     5. Print test result
    ///
    /// Note: The reference files are not committed, so save space.
    /// You can always check out an older tag and create the reference files.
    /// </summary>
    private static async Task Main(string[] args)
    {
        var referenceFolder = @"d:\\ApprovalTests\\References";
        var gitCloneFolder = @"d:\\ApprovalTests\\Repositories";

        if (args.Length == 2)
        {
            referenceFolder = args[0];
            gitCloneFolder = args[1];
        }
        else
        {
            Console.WriteLine("Usage: TestTool <reference-folder> <git-clone-folder>");
            Console.WriteLine("Using default folders.");
        }


        Console.WriteLine("Use reference folder: " + referenceFolder);
        Console.WriteLine("Use git clone folder: " + gitCloneFolder);


        EnsureDirectoryExists(referenceFolder);
        EnsureDirectoryExists(gitCloneFolder);

        var executablePath = Assembly.GetExecutingAssembly().Location;
        var executableDirectory = Path.GetDirectoryName(executablePath);
        var repoFile = Path.Combine(executableDirectory, "repositories.txt");

        if (!File.Exists(repoFile))
        {
            Console.WriteLine($"Input file 'repositories.txt' not found in {executableDirectory}");
            return;
        }


        Initializer.InitializeMsBuildLocator();

        foreach (var line in File.ReadLines(repoFile))
        {
            var parts = line.Split(',');
            if (parts.Length != 3)
            {
                Console.WriteLine($"Invalid input line: {line}");
                continue;
            }

            var repoUrl = parts[0];
            var slnRelativePath = parts[1];
            var commitHash = parts[2];

            await ProcessRepository(repoUrl, slnRelativePath, commitHash, gitCloneFolder, referenceFolder);
        }
    }

    private static async Task ProcessRepository(string repoUrl, string slnRelativePath, string commitHash,
        string gitCloneFolder, string referenceFolder)
    {
        var repoName = Path.GetFileNameWithoutExtension(repoUrl);
        var repoPath = Path.Combine(gitCloneFolder, repoName);

        // Clone or pull the repository
        if (!Directory.Exists(repoPath))
        {
            Repository.Clone(repoUrl, repoPath);
        }
        else
        {
            using (var repo = new Repository(repoPath))
            {
                Commands.Fetch(repo, "origin", Array.Empty<string>(), new FetchOptions(), null);
            }
        }

        // Checkout the specified commit
        using (var repo = new Repository(repoPath))
        {
            var commit = repo.Lookup<Commit>(commitHash);
            if (commit == null)
            {
                Console.WriteLine($"Commit {commitHash} not found in repository {repoName}");
                return;
            }

            Commands.Checkout(repo, commit);
        }

        // Generate paths
        var slnPath = Path.Combine(repoPath, slnRelativePath);
        var outputFileName = $"{commitHash}.txt";
        var outputPath = Path.Combine(gitCloneFolder, outputFileName);

        // Run test code (placeholder)
        await RunTestCode(slnPath, outputPath);

        // Compare output with reference or copy to reference folder
        var referencePath = Path.Combine(referenceFolder, outputFileName);
        if (File.Exists(referencePath))
        {
            var areEqual = CompareFiles(outputPath, referencePath);
            PrintColoredTestResult(repoName, commitHash, areEqual);
        }
        else
        {
            File.Copy(outputPath, referencePath);
            Console.WriteLine($"No reference file for {repoName} at {commitHash}. Created new reference file.");
        }
    }

    private static async Task RunTestCode(string slnPath, string outputPath)
    {
        var parserConfig = new ParserConfig(new ProjectExclusionRegExCollection(), 1);
        var parser = new Parser(parserConfig);
        var graph = await parser.ParseSolution(slnPath);
        await File.WriteAllTextAsync(outputPath, graph.ToDebug());
    }

    private static bool CompareFiles(string file1, string file2)
    {
        using var stream1 = File.OpenRead(file1);
        using var stream2 = File.OpenRead(file2);
        if (stream1.Length != stream2.Length)
        {
            return false;
        }

        using var reader1 = new StreamReader(stream1);
        using var reader2 = new StreamReader(stream2);
        while (reader1.ReadLine() is { } line1)
        {
            var line2 = reader2.ReadLine();
            if (line2 == null || line1 != line2)
            {
                return false;
            }
        }

        // Since the files have the same length that should never happen.
        return reader2.ReadLine() == null;
    }

    private static void PrintColoredTestResult(string repoName, string commitHash, bool passed)
    {
        Console.Write($"Test for {repoName} at {commitHash}: ");
        Console.ForegroundColor = passed ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine(passed ? "Passed" : "Failed");
        Console.ResetColor();
    }


    private static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            try
            {
                Directory.CreateDirectory(path);
                Console.WriteLine($"Created directory: {path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating directory {path}: {ex.Message}");
                Environment.Exit(1);
            }
        }
    }
}