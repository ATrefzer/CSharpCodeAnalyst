using System.Text;
using CSharpCodeAnalyst.History.Model;
using CSharpCodeAnalyst.History.System;

namespace CSharpCodeAnalyst.History.Git;

public sealed class GitCommandLine
{
    // Git gives up the inexact rename detection for a commit with more candidates than this.
    private const int RenameLimit = 99999;

    private readonly ProcessRunner _runner;
    private readonly string _workingDirectory;

    public GitCommandLine(string workingDirectory)
    {
        _workingDirectory = workingDirectory;
        _runner = new ProcessRunner { DefaultEncoding = Encoding.UTF8 };
    }

    public string Annotate(string localPath)
    {
        // git pull origin master
        var program = "git";
        var args = $"annotate \"{localPath}\"";
        return ExecuteCommandLine(program, args).StdOut;
    }

    public void ExportFileRevision(string serverPath, string revision, string exportFile)
    {
        var program = "git";

        var args = $"show {revision}:\"{serverPath}\"";

        var result = ExecuteCommandLine(program, args);
        File.WriteAllText(exportFile, result.StdOut);
    }

    public string GetAllTrackedFiles(string? hash = null)
    {
        var program = "git";

        if (hash == null)
        {
            hash = "HEAD";
        }

        // -z: NUL terminated and verbatim. Without it git quotes and escapes unusual paths.
        var args = $"ls-tree -r --name-only -z {hash}";

        var result = ExecuteCommandLine(program, args);
        return result.StdOut;
    }

    /// <summary>
    ///     Returns true if there are any changes in the working or
    ///     staging area. Detects un-tracked changes
    /// </summary>
    public bool HasLocalChanges()
    {
        const string program = "git";
        const string args = "status --short";
        var result = ExecuteCommandLine(program, args);
        return !string.IsNullOrEmpty(result.StdOut.Trim());
    }

    /// <summary>
    ///     Returns true if there are local commits not pushed to the remote.
    /// </summary>
    public bool HasLocalCommits()
    {
        var hint = "your branch is ahead of";
        var program = "git";
        var args = "status";
        var result = ExecuteCommandLine(program, args);
        return !result.StdOut.ToLowerInvariant().Contains(hint);
    }


    public bool HasIndexOrWorkspaceChanges()
    {
        // https://stackoverflow.com/questions/3882838/whats-an-easy-way-to-detect-modified-files-in-a-git-workspace

        // ... check both the staged contents (what is in the index) and the files in the working tree.
        // Alternatives like git ls-files -m will only check the working tree against the index
        // (i.e. they will ignore any staged (but uncommitted) content that is also in the working tree)

        var program = "git";
        var args = "diff-index -M -C --quiet HEAD";

        // A non zero exit code means "changes found" here, so don't treat it as an error.
        var result = ExecuteCommandLine(program, args, false);

        // Exit code 0 = no changes
        return result.ExitCode != 0;
    }


    /// <summary>
    ///     The whole history reachable from HEAD in a single call, newest first, in the format
    ///     of <see cref="GitLogReader" />. The output is streamed because it can get large.
    /// </summary>
    public void ReadLog(Action<TextReader> readLog)
    {
        // Everything that shapes the output is given explicitly. Otherwise the result depends
        // on the configuration of the user (diff.renames, log.diffMerges, log.showSignature ...).
        //
        // --topo-order   No parent before all of its children. The reader of the log relies on it.
        // -M             Detect renames.
        // -m             For a merge, the diff against each parent (log.diffMerges=separate).
        //                By default git log shows no diff for merges at all.
        // -z             NUL terminated, paths verbatim.
        var args = $"-c diff.renameLimit={RenameLimit} -c log.diffMerges=separate -c log.showRoot=true " +
                   "log --topo-order -m -M --name-status -z --no-color --no-show-signature --encoding=UTF-8 " +
                   $"--format={GitLogReader.Format}";

        var result = _runner.RunProcess("git", args, _workingDirectory, readLog);
        ThrowOnError("git", args, result);
    }

    /// <summary>
    ///     Number of commits reachable from HEAD.
    /// </summary>
    public int GetCommitCount()
    {
        var result = ExecuteCommandLine("git", "rev-list --count HEAD");
        return int.Parse(result.StdOut.Trim());
    }

    private ProcessResult ExecuteCommandLine(string program, string args, bool throwOnError = true)
    {
        var result = _runner.RunProcess(program, args, _workingDirectory);
        if (throwOnError)
        {
            ThrowOnError(program, args, result);
        }

        return result;
    }

    private static void ThrowOnError(string program, string args, ProcessResult result)
    {
        // Decide by exit code, not by stderr. Git writes harmless warnings
        // (line ending hints, rename limit etc.) to stderr while succeeding.
        if (result.ExitCode != 0)
        {
            var message = string.IsNullOrEmpty(result.StdErr)
                ? $"'{program} {args}' failed with exit code {result.ExitCode}."
                : result.StdErr;
            throw new ProviderException(message);
        }
    }

}