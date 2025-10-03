using System.Reflection;
using CodeParser.Parser;
using CodeParser.Parser.Config;
using LibGit2Sharp;

namespace ApprovalTestTool;

internal static class TestTool
{
    /// <summary>
    ///     Automatic approval tool
    ///
    ///     Prerequisites:
    ///     Manually copy the latest known reference digest from ApprovalTestTool/References
    ///     to the reference folder if you want to use them.
    /// 
    ///     For each line in the repositories.txt
    ///     1. Clone or pull the repository
    ///     2. Checkout the specified commit
    ///     3. Run test code: Parse the solution and write the output (digest) to a file.
    ///     4. Compare output (digest) with reference or copy it to reference folder if missing.
    ///     5. Print test result
    ///     You can always check out an older tag and create the reference files.
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
        var executableDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty;
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
            using var repo = new Repository(repoPath);
            Commands.Fetch(repo, "origin", [], new FetchOptions(), null);
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
        var digestFileName = $"{commitHash}_digest.txt";
        var outputPath = Path.Combine(gitCloneFolder, outputFileName);
        var outputDigestPath = Path.Combine(gitCloneFolder, digestFileName);

        // Run test code (placeholder)
        await RunTestCode(slnPath, outputPath);

        // Generate digest
        // So we generate the full output and a digest file to store in GIT.
        var text = await File.ReadAllTextAsync(outputPath);
        var digest = Hash.ComputeHash(text);
        await File.WriteAllTextAsync(outputDigestPath, digest);

        // Compare digest output with reference or copy missing files to reference folder
        var referenceDigestPath = Path.Combine(referenceFolder, digestFileName);
        var referenceOutputPath = Path.Combine(referenceFolder, outputFileName);
        if (File.Exists(referenceDigestPath))
        {
            var areEqual = CompareFiles(outputDigestPath, referenceDigestPath);
            PrintColoredTestResult(repoName, commitHash, areEqual);
        }
        else
        {
            File.Copy(outputDigestPath, referenceDigestPath, true);
            File.Copy(outputPath, referenceOutputPath, true);
            Console.WriteLine($"No reference file for {repoName} at {commitHash}. Created new reference file.");
        }
    }

    private static async Task RunTestCode(string slnPath, string outputPath)
    {
        var parserConfig = new ParserConfig(new ProjectExclusionRegExCollection(), false);
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
        while (reader1.ReadLine() is {} line1)
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
        if (Directory.Exists(path))
        {
            return;
        }

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