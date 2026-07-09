using System.Text;
using CSharpCodeAnalyst.History.Model;
using CSharpCodeAnalyst.History.System;

namespace CSharpCodeAnalyst.History.Git;

public sealed class GitCommandLine
{
    // Note: Use Committer Date. Otherwise, children of a commit may appear before the parent.

    /// <summary>
    ///     %H   Hash (abbreviated is %h)
    ///     %n   Newline
    ///     %aN  Author name
    ///     %cN  Committer name
    ///     %ad  Author date (format respects --date= option)
    ///     %cd  Committer date (format respects --date= option)
    ///     %s   Subject (commit message)
    ///     %P   Parents (all sha1s in one line) First commit does not have a parent!
    ///     Log of the whole branch or a single file shall have the same output for easier parsing.
    /// </summary>
    private const string LogFormat = "START_HEADER%n%H%n%aN%n%cd%n%P%n%s%nEND_HEADER";

    private const string MainBranch = "master";
    private readonly string _branch;
    private readonly ProcessRunner _runner;
    private readonly string _workingDirectory;

    public GitCommandLine(string workingDirectory)
    {
        _workingDirectory = workingDirectory;
        _runner = new ProcessRunner { DefaultEncoding = Encoding.UTF8 };
        _branch = GetCheckedOutBranch();
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

        // Optional HEAD
        var args = $"ls-tree -r --name-only {hash}";

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


    public string LogWithoutRenames()
    {
        // git log --all --numstat --date=short --pretty=format:'--%h--%ad--%aN' --no-renames 
        var program = "git";

        var args = $"log --pretty=format:{LogFormat} --date=iso-strict-local --name-status --no-renames";

        //-c diff.renameLimit=99999 log --pretty=format:{START_HEADER%n%H%n%aN%n%cd%n%P%n%s%nEND_HEADER --date=iso-strict --name-status --simplify-merges --full-history

        var result = ExecuteCommandLine(program, args);
        return result.StdOut;
    }

    public string Log()
    {
        // --num_stat Would shows added and removed lines

        var program = "git";

        // Full history, simplify merges. Renames are tracked by default.
        // We could use --find-renames -M9 to change the similarity to 90% but to results are not so good
        //
        // --cc implies the -c option and further compresses merge commits.
        //
        var args = $"-c diff.renameLimit=99999 log --pretty=format:{LogFormat} --date=iso-strict-local --name-status --simplify-merges --full-history";

        var result = ExecuteCommandLine(program, args);
        return result.StdOut;
    }

    /// <summary>
    ///     Git by default simplifies the history for a single file. This means the parent relationships may be incomplete.
    ///     Renames are tracked. Merge commits that do not contribute are not part of the history.
    /// </summary>
    public string Log(string localPath)
    {
        localPath = localPath.Replace("\\", "/");
        const string program = "git";

        // --follow to track renaming, works only for a single file!
        // When --follow is used --full-history has no effect. We don't see merge commits that do contribute to the file.

        // I had a single case where --follow caused the output to be empty!

        var args = $"log --follow --pretty=format:{LogFormat} --date=iso-strict-local --name-status -- \"{localPath}\"";

        var result = ExecuteCommandLine(program, args);
        return result.StdOut;
    }


    public string GetCheckedOutBranch()
    {
        var program = "git";
        var args = "symbolic-ref --short -q HEAD";

        // Exits with 1 (and no output) on a detached HEAD. Not an error here.
        var result = ExecuteCommandLine(program, args, false);
        return result.StdOut.Trim();
    }

    public bool IsMasterGetCheckedOut()
    {
        const string program = "git";
        const string args = "symbolic-ref --short -q HEAD";
        var result = ExecuteCommandLine(program, args, false);
        return string.Compare(result.StdOut.Trim('\n'), _branch, StringComparison.OrdinalIgnoreCase) == 0;
    }

    private ProcessResult ExecuteCommandLine(string program, string args, bool throwOnError = true)
    {
        var result = _runner.RunProcess(program, args, _workingDirectory);

        // Decide by exit code, not by stderr. Git writes harmless warnings
        // (line ending hints, rename limit etc.) to stderr while succeeding.
        if (throwOnError && result.ExitCode != 0)
        {
            var message = string.IsNullOrEmpty(result.StdErr)
                ? $"'{program} {args}' failed with exit code {result.ExitCode}."
                : result.StdErr;
            throw new ProviderException(message);
        }

        return result;
    }

    /// <summary>
    ///     Returns the hash of the commit HEAD points to.
    ///     Note: We must not read .git\refs\heads\... directly. The loose ref file
    ///     disappears as soon as git packs the refs (git gc).
    /// </summary>
    public string GetHeadHash()
    {
        const string program = "git";
        const string args = "rev-parse HEAD";
        var result = ExecuteCommandLine(program, args);
        return result.StdOut.Trim();
    }
}