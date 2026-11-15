using System.Diagnostics;
using System.Text;

namespace CodeParserTests.UnitTests.History;

/// <summary>
///     A throw-away git repository in the temp directory, built with the git command line.
///     Commits are created without the configuration of the machine (no global or system
///     config) and with fixed dates, so a test builds the same repository everywhere.
/// </summary>
internal sealed class TestGitRepository : IDisposable
{
    private readonly string _root;
    private readonly string _emptyGlobalConfig;
    private DateTimeOffset _clock = new(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public TestGitRepository()
    {
        _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "csca-git-" + Guid.NewGuid().ToString("N"));
        Path = System.IO.Path.Combine(_root, "repo");
        Directory.CreateDirectory(Path);

        _emptyGlobalConfig = System.IO.Path.Combine(_root, "gitconfig");
        File.WriteAllText(_emptyGlobalConfig, string.Empty);

        Git("init", "-q", "-b", "main");

        // Keep the content exactly as written. Otherwise git warns about line endings.
        Git("config", "core.autocrlf", "false");
    }

    public string Path { get; }

    public static bool IsGitAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("git", "--version")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
            });
            process!.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    ///     Some lines of text. Git only detects a rename if enough of the content stays the same.
    /// </summary>
    public static string Content(string topic, int lines = 40)
    {
        return string.Concat(Enumerable.Range(1, lines).Select(i => $"line {i} of {topic}\n"));
    }

    public void Write(string relativePath, string content)
    {
        var file = ToLocalPath(relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content, new UTF8Encoding(false));
    }

    public void Append(string relativePath, string text)
    {
        File.AppendAllText(ToLocalPath(relativePath), text, new UTF8Encoding(false));
    }

    public void Move(string from, string to)
    {
        var target = ToLocalPath(to);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
        File.Move(ToLocalPath(from), target);
    }

    public void Delete(string relativePath)
    {
        File.Delete(ToLocalPath(relativePath));
    }

    /// <summary>
    ///     Commits everything in the working directory. The message identifies the commit in the assertions.
    /// </summary>
    public void Commit(string message)
    {
        Git("add", "-A");
        Git("commit", "-q", "--allow-empty", "-m", message);
    }

    public void CreateBranch(string name)
    {
        Git("checkout", "-q", "-b", name);
    }

    public void Checkout(string name)
    {
        Git("checkout", "-q", name);
    }

    /// <summary>
    ///     Merges the branch into the current one, always with a merge commit.
    ///     If the merge conflicts, <paramref name="resolve" /> has to bring the working directory into the wanted state.
    /// </summary>
    public void Merge(string branch, string message, Action? resolve = null)
    {
        // Exit code 1 means conflict. Whatever is in the working directory afterwards is committed as the result.
        Run(false, "merge", "--no-ff", "--no-commit", branch);
        resolve?.Invoke();
        Commit(message);
    }

    public void Git(params string[] arguments)
    {
        Run(true, arguments);
    }

    private void Run(bool throwOnError, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = Path,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // Every commit a minute later than the one before.
        _clock = _clock.AddMinutes(1);
        var date = _clock.ToString("yyyy-MM-ddTHH:mm:sszzz");

        var environment = startInfo.Environment;
        environment["GIT_CONFIG_NOSYSTEM"] = "1";
        environment["GIT_CONFIG_GLOBAL"] = _emptyGlobalConfig;
        environment["GIT_AUTHOR_NAME"] = "Test Author";
        environment["GIT_AUTHOR_EMAIL"] = "author@example.com";
        environment["GIT_COMMITTER_NAME"] = "Test Author";
        environment["GIT_COMMITTER_EMAIL"] = "author@example.com";
        environment["GIT_AUTHOR_DATE"] = date;
        environment["GIT_COMMITTER_DATE"] = date;

        using var process = Process.Start(startInfo)!;
        var stdErr = process.StandardError.ReadToEndAsync();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (throwOnError && process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {stdErr.Result}");
        }
    }

    private string ToLocalPath(string relativePath)
    {
        return System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
    }

    public void Dispose()
    {
        if (!Directory.Exists(_root))
        {
            return;
        }

        // Git marks its object files read-only. Directory.Delete refuses those.
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_root, true);
    }
}
