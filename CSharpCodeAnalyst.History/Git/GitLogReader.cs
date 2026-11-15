using System.Globalization;
using System.Text;
using CSharpCodeAnalyst.History.Model;

namespace CSharpCodeAnalyst.History.Git;

/// <summary>
///     One changed file in the diff of a commit against one of its parents.
/// </summary>
/// <param name="Kind">What happened to the file.</param>
/// <param name="Path">Path after the commit. For a delete the path that was removed.</param>
/// <param name="FromPath">Path before the commit. Only set for renames and copies.</param>
public sealed record LogChange(KindOfChange Kind, string Path, string? FromPath = null);

/// <summary>
///     One commit as reported by <see cref="GitCommandLine.ReadLog" />.
/// </summary>
public sealed class LogCommit
{
    public required string Hash { get; init; }

    public required IReadOnlyList<string> Parents { get; init; }

    /// <summary>
    ///     Committer date in local time.
    /// </summary>
    public required DateTime Date { get; init; }

    public required string Author { get; init; }

    public required string Subject { get; init; }

    /// <summary>
    ///     The diff against each parent the commit differs from, in parent order.
    ///     Git omits the diff against a parent the commit is identical to. So a merge that
    ///     differs from every parent has exactly as many entries as it has parents.
    ///     The diff of a root commit is the one against the empty tree.
    /// </summary>
    public List<List<LogChange>> Diffs { get; } = [];

    public bool IsMerge
    {
        get => Parents.Count > 1;
    }
}

/// <summary>
///     Reads the output of <see cref="GitCommandLine.ReadLog" />.
///     The format is defined here because format and reader have to match.
///     <code>
///     \x01 hash \x02 parents \x02 date \x02 author \x02 subject \x03 \0
///     \n status \0 path \0                 (add, modify, delete, type change)
///        status \0 old path \0 path \0     (rename, copy)
///     </code>
///     With -z git terminates every field with NUL and prints paths verbatim.
///     So there is no quoting to undo and no character a path could contain to confuse us.
///     For a merge commit (-m) git repeats the header once per parent the merge differs from.
/// </summary>
public static class GitLogReader
{
    private const char RecordStart = '\x01';
    private const char FieldSeparator = '\x02';
    private const char RecordEnd = '\x03';
    private const int HeaderFieldCount = 5;

    /// <summary>
    ///     Hash, parents, committer date (strict ISO 8601), author (respecting .mailmap), subject.
    ///     Committer date, not author date: after a rebase or cherry-pick the author date may be
    ///     older than the parent commit.
    /// </summary>
    public const string Format = "%x01%H%x02%P%x02%cI%x02%aN%x02%s%x03";

    /// <summary>
    ///     Returns the commits in the order git prints them.
    ///     A truncated stream (the git process died) ends the enumeration. The commit that was
    ///     cut off is dropped, the complete ones before it are kept. Whether the output was
    ///     complete is decided by the caller via the exit code of git.
    /// </summary>
    public static IEnumerable<LogCommit> Read(TextReader reader)
    {
        LogCommit? current = null;
        var startNewDiff = true;
        var buffer = new StringBuilder();

        while (ReadToken(reader, buffer) is { } token)
        {
            if (token.Length > 0 && token[0] == RecordStart)
            {
                var commit = ParseHeader(token);
                if (commit == null)
                {
                    yield break;
                }

                startNewDiff = true;
                if (current != null && current.Hash == commit.Hash)
                {
                    // Same merge commit again, now compared to its next parent.
                    continue;
                }

                if (current != null)
                {
                    yield return current;
                }

                current = commit;
                continue;
            }

            // Only the first status after a header is preceded by a line break.
            var status = token.TrimStart('\n');
            if (status.Length == 0)
            {
                continue;
            }

            if (current == null)
            {
                // Not our format.
                yield break;
            }

            var kind = ToKindOfChange(status);
            var path = ReadToken(reader, buffer);
            string? fromPath = null;
            if (path != null && kind is KindOfChange.Rename or KindOfChange.Copy)
            {
                fromPath = path;
                path = ReadToken(reader, buffer);
            }

            if (path == null)
            {
                // Cut off in the middle of an entry.
                yield break;
            }

            if (startNewDiff)
            {
                current.Diffs.Add([]);
                startNewDiff = false;
            }

            current.Diffs[^1].Add(new LogChange(kind, path, fromPath));
        }

        if (current != null)
        {
            yield return current;
        }
    }

    /// <summary>
    ///     Returns the text up to the next NUL. Null if the stream ends before the NUL:
    ///     every token git writes is terminated, so anything else was cut off.
    /// </summary>
    private static string? ReadToken(TextReader reader, StringBuilder buffer)
    {
        buffer.Clear();
        while (true)
        {
            var next = reader.Read();
            if (next < 0)
            {
                return null;
            }

            if (next == '\0')
            {
                return buffer.ToString();
            }

            buffer.Append((char)next);
        }
    }

    private static LogCommit? ParseHeader(string token)
    {
        if (token[^1] != RecordEnd)
        {
            return null;
        }

        var fields = token[1..^1].Split(FieldSeparator, HeaderFieldCount);
        if (fields.Length != HeaderFieldCount)
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(fields[2], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return null;
        }

        return new LogCommit
        {
            Hash = fields[0],
            Parents = fields[1].Split(' ', StringSplitOptions.RemoveEmptyEntries),
            Date = date.LocalDateTime,
            Author = fields[3],
            Subject = fields[4]
        };
    }

    private static KindOfChange ToKindOfChange(string status)
    {
        // Renames and copies are followed by the similarity (R087).
        return status[0] switch
        {
            'A' => KindOfChange.Add,
            'M' => KindOfChange.Edit,
            'D' => KindOfChange.Delete,
            'R' => KindOfChange.Rename,
            'C' => KindOfChange.Copy,
            'T' => KindOfChange.TypeChanged,
            _ => KindOfChange.None
        };
    }
}
