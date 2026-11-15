using CSharpCodeAnalyst.History.Git;
using CSharpCodeAnalyst.History.Model;

namespace CodeParserTests.UnitTests.History;

/// <summary>
///     Tests for <see cref="FileIdentityTracker" /> with hand-built commits, fed newest first
///     like git prints them. No repository involved.
/// </summary>
[TestFixture]
public class FileIdentityTrackerTests
{
    private string[] _filesAtHead = [];

    private FileIdentityTracker CreateTracker(params string[] filesAtHead)
    {
        _filesAtHead = filesAtHead;
        return new FileIdentityTracker(filesAtHead, new PathMapper(@"C:\repo"));
    }

    private static LogCommit Regular(string hash, params LogChange[] changes)
    {
        var commit = new LogCommit { Hash = hash, Parents = ["parent"], Author = "Alice", Subject = hash, Date = DateTime.Now };
        if (changes.Length > 0)
        {
            commit.Diffs.Add(changes.ToList());
        }

        return commit;
    }

    /// <summary>
    ///     A merge of two parents. One diff per parent the merge differs from.
    /// </summary>
    private static LogCommit Merge(string hash, params LogChange[][] diffs)
    {
        var commit = new LogCommit { Hash = hash, Parents = ["parent1", "parent2"], Author = "Alice", Subject = hash, Date = DateTime.Now };
        commit.Diffs.AddRange(diffs.Select(diff => diff.ToList()));
        return commit;
    }

    private static LogChange Add(string path)
    {
        return new LogChange(KindOfChange.Add, path);
    }

    private static LogChange Edit(string path)
    {
        return new LogChange(KindOfChange.Edit, path);
    }

    private static LogChange Delete(string path)
    {
        return new LogChange(KindOfChange.Delete, path);
    }

    private static LogChange Rename(string from, string to)
    {
        return new LogChange(KindOfChange.Rename, to, from);
    }

    /// <summary>
    ///     Feeds the commits and returns, per file at HEAD, the commits that changed it.
    /// </summary>
    private Dictionary<string, List<string>> Replay(FileIdentityTracker tracker, params LogCommit[] newestFirst)
    {
        var changeSets = newestFirst.Select(tracker.Apply).OfType<ChangeSet>().ToList();

        // The newest item of an id that uses a name of HEAD tells which file the id stands for.
        var result = new Dictionary<string, List<string>>();
        foreach (var group in changeSets.SelectMany(cs => cs.Items.Select(item => (cs.Id, item))).GroupBy(x => x.item.Id!))
        {
            var pathAtHead = group.Select(x => x.item.ServerPath).First(path => _filesAtHead.Contains(path));
            result[pathAtHead] = group.Select(x => x.Id).ToList();
        }

        return result;
    }

    [Test]
    public void Rename_HistoryContinuesUnderTheOldName()
    {
        var tracker = CreateTracker("Core/TextHelper.cs");

        var history = Replay(tracker,
            Regular("d", Edit("Core/TextHelper.cs")),
            Regular("c", Rename("Utils/Helper.cs", "Core/TextHelper.cs")),
            Regular("b", Edit("Utils/Helper.cs")),
            Regular("a", Add("Utils/Helper.cs")));

        Assert.That(history["Core/TextHelper.cs"], Is.EqualTo(new[] { "d", "c", "b", "a" }));
    }

    [Test]
    public void Apply_CreatesItemsWithIdAndPathsOfThatTime()
    {
        var tracker = CreateTracker("new.cs");

        var renamed = tracker.Apply(Regular("b", Rename("old.cs", "new.cs")))!.Items.Single();
        var added = tracker.Apply(Regular("a", Add("old.cs")))!.Items.Single();

        Assert.That(renamed.Kind, Is.EqualTo(KindOfChange.Rename));
        Assert.That(renamed.ServerPath, Is.EqualTo("new.cs"));
        Assert.That(renamed.FromServerPath, Is.EqualTo("old.cs"));
        Assert.That(renamed.LocalPath, Is.EqualTo(@"C:\repo\new.cs"));

        Assert.That(added.Kind, Is.EqualTo(KindOfChange.Add));
        Assert.That(added.ServerPath, Is.EqualTo("old.cs"));
        Assert.That(added.Id, Is.EqualTo(renamed.Id));
    }

    [Test]
    public void FilesGoneToday_AreNotPartOfTheHistory()
    {
        var tracker = CreateTracker("alive.cs");

        var onlyDeadFile = tracker.Apply(Regular("b", Edit("dead.cs")));
        var both = tracker.Apply(Regular("a", Add("dead.cs"), Add("alive.cs")));

        Assert.That(onlyDeadFile, Is.Null);
        Assert.That(both!.Items.Single().ServerPath, Is.EqualTo("alive.cs"));
    }

    [Test]
    public void DeletedAndCreatedAgain_NewFileDoesNotInheritTheOldHistory()
    {
        var tracker = CreateTracker("Program.cs");

        var history = Replay(tracker,
            Regular("d", Add("Program.cs")),
            Regular("c", Delete("Program.cs")),
            Regular("b", Edit("Program.cs")),
            Regular("a", Add("Program.cs")));

        Assert.That(history["Program.cs"], Is.EqualTo(new[] { "d" }));
    }

    [Test]
    public void NameReusedAfterRename_OldHistoryBelongsToTheRenamedFile()
    {
        // Helper.cs was renamed to TextHelper.cs, later a new and unrelated Helper.cs was created.
        var tracker = CreateTracker("Helper.cs", "TextHelper.cs");

        var history = Replay(tracker,
            Regular("d", Add("Helper.cs")),
            Regular("c", Rename("Helper.cs", "TextHelper.cs")),
            Regular("b", Edit("Helper.cs")),
            Regular("a", Add("Helper.cs")));

        Assert.That(history["Helper.cs"], Is.EqualTo(new[] { "d" }));
        Assert.That(history["TextHelper.cs"], Is.EqualTo(new[] { "c", "b", "a" }));
        Assert.That(tracker.Warnings, Is.Empty, "A reused name is not ambiguous.");
    }

    [Test]
    public void RenameOnBranch_OldNameIsKnownBeforeTheBranchCommitsArrive()
    {
        // The rename happened on the feature branch. The merge compared to main already shows
        // it, so the commit on main that still uses the old name is assigned correctly even
        // though it is fed before the rename commit of the branch.
        var tracker = CreateTracker("ReportWriter.cs");

        var history = Replay(tracker,
            Merge("m", [Rename("Report.cs", "ReportWriter.cs")]),
            Regular("main", Edit("Report.cs")),
            Regular("feature", Rename("Report.cs", "ReportWriter.cs")),
            Regular("a", Add("Report.cs")));

        Assert.That(history["ReportWriter.cs"], Is.EqualTo(new[] { "main", "feature", "a" }));
    }

    [Test]
    public void Merge_OnlyCountsForFilesThatDifferFromEveryParent()
    {
        var tracker = CreateTracker("conflict.cs", "fromBranch.cs", "fromMain.cs");

        var merge = tracker.Apply(Merge("m",
            [Edit("conflict.cs"), Add("fromBranch.cs")],
            [Edit("conflict.cs"), Edit("fromMain.cs")]));

        Assert.That(merge!.Items.Single().ServerPath, Is.EqualTo("conflict.cs"));
    }

    [Test]
    public void Merge_IdenticalToOneParent_IsNotPartOfTheHistory()
    {
        // Git omits the diff against the parent the merge is identical to.
        var tracker = CreateTracker("fromBranch.cs");

        var merge = tracker.Apply(Merge("m", [Add("fromBranch.cs")]));

        Assert.That(merge, Is.Null);
    }

    [Test]
    public void SameFileAddedOnTwoBranches_BothCount()
    {
        // For example a cherry-pick. An add says nothing about the history of the other branch.
        var tracker = CreateTracker("X.cs");

        var history = Replay(tracker,
            Regular("d", Edit("X.cs")),
            Regular("addOnFeature", Add("X.cs")),
            Regular("editOnMain", Edit("X.cs")),
            Regular("addOnMain", Add("X.cs")));

        Assert.That(history["X.cs"], Is.EqualTo(new[] { "d", "addOnFeature", "editOnMain", "addOnMain" }));
    }

    [Test]
    public void TwoNamesOfTheSameFileInOneCommit_CountOnce()
    {
        var tracker = CreateTracker("new.cs");
        tracker.Apply(Merge("m", [Rename("old.cs", "new.cs")]));

        var changeSet = tracker.Apply(Regular("a", Edit("old.cs"), Edit("new.cs")));

        Assert.That(changeSet!.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public void RenamedDifferentlyOnTwoBranches_WarnsAboutTheAmbiguity()
    {
        // Both files go back to Origin.cs. Only one can inherit the history before the split.
        var tracker = CreateTracker("Left.cs", "Right.cs");

        var history = Replay(tracker,
            Regular("left", Rename("Origin.cs", "Left.cs")),
            Regular("right", Rename("Origin.cs", "Right.cs")),
            Regular("a", Add("Origin.cs")));

        Assert.That(history["Left.cs"], Is.EqualTo(new[] { "left" }));
        Assert.That(history["Right.cs"], Is.EqualTo(new[] { "right", "a" }));

        var warning = tracker.Warnings.Single();
        Assert.That(warning.Commit, Is.EqualTo("right"));
        Assert.That(warning.Warning, Does.Contain("Origin.cs").And.Contain("Left.cs").And.Contain("Right.cs"));
    }

    [Test]
    public void GetFilesWithoutHistory_ReturnsFilesNoCommitWasFoundFor()
    {
        var tracker = CreateTracker("known.cs", "unknown.cs");

        tracker.Apply(Regular("a", Add("known.cs")));

        Assert.That(tracker.GetFilesWithoutHistory(), Is.EqualTo(new[] { "unknown.cs" }));
    }
}
