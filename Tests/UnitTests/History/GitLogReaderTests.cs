using CSharpCodeAnalyst.History.Git;
using CSharpCodeAnalyst.History.Model;

namespace CodeParserTests.UnitTests.History;

/// <summary>
///     Tests for <see cref="GitLogReader" /> against hand-written output in the format of
///     "git log -z --name-status". That real git output has this shape is pinned by
///     <see cref="GitProviderTests" />.
/// </summary>
[TestFixture]
public class GitLogReaderTests
{
    private static string Header(string hash, string parents, string subject = "subject", string author = "Alice",
        string date = "2024-05-01T10:00:00+00:00")
    {
        return $"\x01{hash}\x02{parents}\x02{date}\x02{author}\x02{subject}\x03\0";
    }

    private static List<LogCommit> Read(string log)
    {
        return GitLogReader.Read(new StringReader(log)).ToList();
    }

    [Test]
    public void Read_RegularCommits_ReturnsThemInTheOrderOfTheLog()
    {
        var log =
            Header("c2", "c1", "Second", "Bob") + "\nM\0src/a.cs\0A\0src/b.cs\0" +
            Header("c1", "", "First") + "\nA\0src/a.cs\0";

        var commits = Read(log);

        Assert.That(commits.Select(c => c.Hash), Is.EqualTo(new[] { "c2", "c1" }));

        var second = commits[0];
        Assert.That(second.Parents, Is.EqualTo(new[] { "c1" }));
        Assert.That(second.Subject, Is.EqualTo("Second"));
        Assert.That(second.Author, Is.EqualTo("Bob"));
        Assert.That(second.Date, Is.EqualTo(DateTimeOffset.Parse("2024-05-01T10:00:00+00:00").LocalDateTime));
        Assert.That(second.Diffs.Single(), Is.EqualTo(new[]
        {
            new LogChange(KindOfChange.Edit, "src/a.cs"),
            new LogChange(KindOfChange.Add, "src/b.cs")
        }));

        Assert.That(commits[1].Parents, Is.Empty);
        Assert.That(commits[1].IsMerge, Is.False);
    }

    [Test]
    public void Read_RenameAndCopy_CarryTheOldPath()
    {
        var log = Header("c1", "c0") + "\nR087\0old/a.cs\0new/a.cs\0C100\0src/b.cs\0src/c.cs\0D\0src/d.cs\0T\0src/e.cs\0";

        var changes = Read(log).Single().Diffs.Single();

        Assert.That(changes, Is.EqualTo(new[]
        {
            new LogChange(KindOfChange.Rename, "new/a.cs", "old/a.cs"),
            new LogChange(KindOfChange.Copy, "src/c.cs", "src/b.cs"),
            new LogChange(KindOfChange.Delete, "src/d.cs"),
            new LogChange(KindOfChange.TypeChanged, "src/e.cs")
        }));
    }

    [Test]
    public void Read_MergeCommit_CollectsOneDiffPerParent()
    {
        // With -m git repeats the header of a merge for every parent it differs from.
        var log =
            Header("m", "p1 p2") + "\nM\0a.cs\0A\0b.cs\0" +
            Header("m", "p1 p2") + "\nM\0a.cs\0" +
            Header("p1", "");

        var commits = Read(log);

        Assert.That(commits, Has.Count.EqualTo(2));
        var merge = commits[0];
        Assert.That(merge.IsMerge, Is.True);
        Assert.That(merge.Diffs, Has.Count.EqualTo(2));
        Assert.That(merge.Diffs[0].Select(c => c.Path), Is.EqualTo(new[] { "a.cs", "b.cs" }));
        Assert.That(merge.Diffs[1].Select(c => c.Path), Is.EqualTo(new[] { "a.cs" }));
    }

    [Test]
    public void Read_CommitWithoutChanges_HasNoDiff()
    {
        var log = Header("c2", "c1") + Header("c1", "") + "\nA\0a.cs\0";

        var commits = Read(log);

        Assert.That(commits[0].Diffs, Is.Empty);
        Assert.That(commits[1].Diffs, Has.Count.EqualTo(1));
    }

    [Test]
    public void Read_UnusualPaths_AreTakenVerbatim()
    {
        // -z prints paths as they are. Blanks, tabs, quotes, line breaks and even our own
        // record marker must not confuse the reader.
        var paths = new[] { "dir with blank/Datei äöü.cs", "tab\there.cs", "\"quoted\".cs", "line\nbreak.cs", "\x01marker.cs" };
        var log = Header("c1", "") + "\n" + string.Concat(paths.Select(path => $"A\0{path}\0"));

        var changes = Read(log).Single().Diffs.Single();

        Assert.That(changes.Select(c => c.Path), Is.EqualTo(paths));
    }

    [Test]
    public void Read_TruncatedInHeader_DropsTheIncompleteCommitWithoutThrowing()
    {
        var log = Header("c2", "c1") + "\nM\0a.cs\0" + "\x01" + "c1\x02";

        var commits = Read(log);

        Assert.That(commits.Single().Hash, Is.EqualTo("c2"));
    }

    [Test]
    public void Read_TruncatedInEntry_DropsTheIncompleteCommitWithoutThrowing()
    {
        // The rename lost its second path. A half read commit would silently miss changes.
        var log = Header("c2", "c1") + "\nM\0a.cs\0" + Header("c1", "") + "\nA\0b.cs\0R100\0old.cs\0";

        var commits = Read(log);

        Assert.That(commits.Single().Hash, Is.EqualTo("c2"));
    }

    [Test]
    public void Read_EmptyOutput_ReturnsNothing()
    {
        Assert.That(Read(string.Empty), Is.Empty);
    }
}
