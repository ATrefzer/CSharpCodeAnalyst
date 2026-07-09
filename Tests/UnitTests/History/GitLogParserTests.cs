using CSharpCodeAnalyst.History.Git;
using GitParser = CSharpCodeAnalyst.History.Git.Parser;

namespace CodeParserTests.UnitTests.History;

/// <summary>
///     Tests for the git-log text parser (<see cref="CSharpCodeAnalyst.History.Git.Parser" />),
///     focused on tolerating truncated / malformed output.
/// </summary>
[TestFixture]
public class GitLogParserTests
{
    // PathMapper is pure (string manipulation only, no filesystem access), so the real one is the
    // simplest and most honest test double - nothing to isolate.
    private static GitParser CreateParser()
    {
        return new GitParser(new PathMapper(@"C:\repo"));
    }

    private const string TwoRecords =
        "START_HEADER\n" +
        "hash1\n" +
        "Alice\n" +
        "2020-01-01\n" +
        "parent1\n" +
        "First comment\n" +
        "END_HEADER\n" +
        "M\tsrc/a.cs\n" +
        "START_HEADER\n" +
        "hash2\n" +
        "Bob\n" +
        "2020-01-02\n" +
        "parent2\n" +
        "Second comment\n" +
        "END_HEADER\n" +
        "A\tsrc/b.cs\n";

    [Test]
    public void ParseLogStringNoGraph_WellFormed_ParsesAllRecords()
    {
        var history = CreateParser().ParseLogStringNoGraph(TwoRecords);

        Assert.That(history.ChangeSets, Has.Count.EqualTo(2));

        // Records are ordered by date descending, so hash2 comes first.
        Assert.That(history.ChangeSets[0].Id, Is.EqualTo("hash2"));
        Assert.That(history.ChangeSets[0].Committer, Is.EqualTo("Bob"));
        Assert.That(history.ChangeSets[0].Items.Single().ServerPath, Is.EqualTo("src/b.cs"));
        Assert.That(history.ChangeSets[1].Id, Is.EqualTo("hash1"));
    }

    [Test]
    public void ParseLogStringNoGraph_TruncatedInHeader_DropsIncompleteRecordWithoutThrowing()
    {
        // The second record breaks off right after the hash - committer / date / parents are
        // missing. Must not throw (previously DateTime.Parse(null)); the incomplete record is
        // dropped and the completed one is kept.
        var log =
            "START_HEADER\n" +
            "hash1\n" +
            "Alice\n" +
            "2020-01-01\n" +
            "parent1\n" +
            "First comment\n" +
            "END_HEADER\n" +
            "M\tsrc/a.cs\n" +
            "START_HEADER\n" +
            "hash2\n"; // truncated here

        var history = CreateParser().ParseLogStringNoGraph(log);

        Assert.That(history.ChangeSets.Single().Id, Is.EqualTo("hash1"));
    }

    [Test]
    public void ParseLogStringNoGraph_CommentNeverClosed_TerminatesInsteadOfHanging()
    {
        // The header is complete but the comment section is never closed by END_HEADER (stream
        // ends). The old ReadComment looped forever here. Run on a worker so a regression fails
        // cleanly (assertion) instead of hanging the whole test run - .NET cannot abort the
        // runaway sync loop, so a plain [Timeout] would not rescue it.
        var log =
            "START_HEADER\n" +
            "hash1\n" +
            "Alice\n" +
            "2020-01-01\n" +
            "parent1\n" +
            "A comment that is never terminated\n"; // no END_HEADER, then EOF

        var task = Task.Run(() => CreateParser().ParseLogStringNoGraph(log));

        Assert.That(task.Wait(TimeSpan.FromSeconds(5)), Is.True, "Parser hung on an unterminated comment.");
        Assert.That(task.Result.ChangeSets, Has.Count.EqualTo(1));
    }
}
