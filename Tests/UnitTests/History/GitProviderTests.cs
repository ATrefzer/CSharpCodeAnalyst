using CSharpCodeAnalyst.History.Git;
using CSharpCodeAnalyst.History.Model;

namespace CodeParserTests.UnitTests.History;

/// <summary>
///     Runs the <see cref="GitProvider" /> against real repositories built on the fly.
///     These tests pin what the pure tests cannot: that git really prints what
///     <see cref="GitLogReader" /> expects, and how the provider behaves for the commit graphs
///     described in Documentation/Design/git-history-provider.md.
///     Commit messages are single letters so the assertions read like the graphs in the comments.
/// </summary>
[TestFixture]
public class GitProviderTests
{
    private TestGitRepository _repo = null!;

    [OneTimeSetUp]
    public void RequireGit()
    {
        if (!TestGitRepository.IsGitAvailable())
        {
            Assert.Ignore("git is not available on this machine.");
        }
    }

    [SetUp]
    public void CreateRepository()
    {
        _repo = new TestGitRepository();
    }

    [TearDown]
    public void DeleteRepository()
    {
        _repo.Dispose();
    }

    /// <summary>
    ///     Messages of the commits that changed the file at HEAD, newest first.
    /// </summary>
    private static List<string> CommitsOf(ChangeSetHistory history, string pathAtHead)
    {
        // The newest item with this path is the file at HEAD.
        var id = history.ChangeSets
            .SelectMany(cs => cs.Items)
            .First(item => item.ServerPath == pathAtHead).Id;

        return history.ChangeSets
            .Where(cs => cs.Items.Any(item => item.Id == id))
            .Select(cs => cs.Comment)
            .ToList();
    }

    private (ChangeSetHistory History, List<WarningMessage> Warnings) Extract()
    {
        var provider = new GitProvider(_repo.Path);
        var history = provider.ExtractChangeSets(null);
        return (history, provider.Warnings);
    }

    [Test]
    public void Rename_HistoryContinuesAcrossTheRename()
    {
        // A (add) -- B (edit) -- C (rename) -- D (edit)
        _repo.Write("Utils/Helper.cs", TestGitRepository.Content("helper"));
        _repo.Commit("A");
        _repo.Append("Utils/Helper.cs", "more\n");
        _repo.Commit("B");
        _repo.Move("Utils/Helper.cs", "Core/TextHelper.cs");
        _repo.Commit("C");
        _repo.Append("Core/TextHelper.cs", "even more\n");
        _repo.Commit("D");

        var (history, warnings) = Extract();

        Assert.That(CommitsOf(history, "Core/TextHelper.cs"), Is.EqualTo(new[] { "D", "C", "B", "A" }));
        Assert.That(warnings, Is.Empty);

        var rename = history.ChangeSets.Single(cs => cs.Comment == "C").Items.Single();
        Assert.That(rename.Kind, Is.EqualTo(KindOfChange.Rename));
        Assert.That(rename.FromServerPath, Is.EqualTo("Utils/Helper.cs"));
        Assert.That(rename.LocalPath, Is.EqualTo(Path.Combine(_repo.Path, "Core", "TextHelper.cs")));
    }

    [Test]
    public void History_IsNewestFirstAndCarriesCommitData()
    {
        _repo.Write("a.txt", "one\n");
        _repo.Commit("A");
        _repo.Append("a.txt", "two\n");
        _repo.Commit("B");

        var (history, _) = Extract();

        Assert.That(history.ChangeSets.Select(cs => cs.Comment), Is.EqualTo(new[] { "B", "A" }));
        var newest = history.ChangeSets[0];
        Assert.That(newest.Committer, Is.EqualTo("Test Author"));
        Assert.That(newest.Id, Has.Length.EqualTo(40));
        Assert.That(newest.Date, Is.GreaterThan(history.ChangeSets[1].Date));
    }

    [Test]
    public void RenameOnBranch_SurvivesTheMerge()
    {
        // main:     A ---- C ---- M ---- D
        //             \         /
        // feature:     B (rename)
        _repo.Write("Report.cs", TestGitRepository.Content("report"));
        _repo.Write("Other.cs", TestGitRepository.Content("other"));
        _repo.Commit("A");

        _repo.CreateBranch("feature");
        _repo.Move("Report.cs", "ReportWriter.cs");
        _repo.Commit("B");

        _repo.Checkout("main");
        _repo.Append("Other.cs", "changed on main\n");
        _repo.Commit("C");

        _repo.Merge("feature", "M");
        _repo.Append("ReportWriter.cs", "changed after the merge\n");
        _repo.Commit("D");

        var (history, warnings) = Extract();

        // The merge only brings in what B did. It is not a change of its own.
        Assert.That(CommitsOf(history, "ReportWriter.cs"), Is.EqualTo(new[] { "D", "B", "A" }));
        Assert.That(history.ChangeSets.Select(cs => cs.Comment), Has.No.Member("M"));
        Assert.That(warnings, Is.Empty);
    }

    [Test]
    public void EditOnMainWhileRenamedOnBranch_BothSidesCount()
    {
        // main:     A ---- C (edit Report.cs) ---- M
        //             \                          /
        // feature:     B (rename to ReportWriter.cs)
        _repo.Write("Report.cs", TestGitRepository.Content("report"));
        _repo.Commit("A");

        _repo.CreateBranch("feature");
        _repo.Move("Report.cs", "ReportWriter.cs");
        _repo.Commit("B");

        _repo.Checkout("main");
        _repo.Write("Report.cs", TestGitRepository.Content("report").Replace("line 3 of report", "line 3 changed on main"));
        _repo.Commit("C");

        _repo.Merge("feature", "M");

        var (history, _) = Extract();

        // C still uses the old name and is found. M counts as well: the merged file differs
        // from both parents, even though git merged it without a conflict.
        Assert.That(CommitsOf(history, "ReportWriter.cs"), Is.EquivalentTo(new[] { "M", "C", "B", "A" }));
    }

    [TestCase("Alpha.cs", "Zebra.cs")]
    [TestCase("Zebra.cs", "Alpha.cs")]
    public void RenamedAndRewrittenOnBranch_DoesNotDependOnTheFileNames(string oldName, string newName)
    {
        // main:     A ---------------- C ---- M ---- D
        //             \                     /
        // feature:     B (rename) -- B2 (rewrite)
        //
        // Compared to main the merge shows a delete and an add, not a rename: too little is
        // left of the old content. The order of these two entries follows the file names.
        _repo.Write(oldName, TestGitRepository.Content("original"));
        _repo.Write("Other.cs", TestGitRepository.Content("other"));
        _repo.Commit("A");

        _repo.CreateBranch("feature");
        _repo.Move(oldName, newName);
        _repo.Commit("B");
        _repo.Write(newName, TestGitRepository.Content("rewritten", 28) + TestGitRepository.Content("original")[^300..]);
        _repo.Commit("B2");

        _repo.Checkout("main");
        _repo.Append("Other.cs", "changed on main\n");
        _repo.Commit("C");

        _repo.Merge("feature", "M");
        _repo.Append(newName, "changed after the merge\n");
        _repo.Commit("D");

        var (history, warnings) = Extract();

        Assert.That(CommitsOf(history, newName), Is.EqualTo(new[] { "D", "B2", "B", "A" }));
        Assert.That(warnings, Is.Empty);
    }

    [Test]
    public void SamePathAddedOnTwoBranches_BothBranchesCount()
    {
        // main:     A ---- C (add X.cs) ---- M (conflict resolved)
        //             \                    /
        // feature:     B (add X.cs) ------
        _repo.Write("Base.cs", "base\n");
        _repo.Commit("A");

        _repo.CreateBranch("feature");
        _repo.Write("X.cs", TestGitRepository.Content("feature"));
        _repo.Commit("B");

        _repo.Checkout("main");
        _repo.Write("X.cs", TestGitRepository.Content("main"));
        _repo.Commit("C");

        _repo.Merge("feature", "M", () => _repo.Write("X.cs", TestGitRepository.Content("resolved")));

        var (history, _) = Extract();

        Assert.That(CommitsOf(history, "X.cs"), Is.EquivalentTo(new[] { "M", "C", "B" }));
    }

    [Test]
    public void CleanMergeOfUnrelatedChanges_IsNotPartOfTheHistory()
    {
        _repo.Write("Main.cs", "main\n");
        _repo.Commit("A");

        _repo.CreateBranch("feature");
        _repo.Write("Feature.cs", "feature\n");
        _repo.Commit("B");

        _repo.Checkout("main");
        _repo.Append("Main.cs", "more\n");
        _repo.Commit("C");

        _repo.Merge("feature", "M");

        var (history, _) = Extract();

        Assert.That(history.ChangeSets.Select(cs => cs.Comment), Is.EquivalentTo(new[] { "A", "B", "C" }));
        Assert.That(CommitsOf(history, "Feature.cs"), Is.EqualTo(new[] { "B" }));
    }

    [Test]
    public void DeletedFiles_AndRecreatedPaths()
    {
        // Gone.cs is deleted for good. Program.cs is deleted and later created again with other content.
        _repo.Write("Gone.cs", TestGitRepository.Content("gone"));
        _repo.Write("Program.cs", TestGitRepository.Content("old program"));
        _repo.Commit("A");
        _repo.Delete("Gone.cs");
        _repo.Delete("Program.cs");
        _repo.Commit("B");
        _repo.Write("Program.cs", TestGitRepository.Content("new program"));
        _repo.Commit("C");

        var (history, _) = Extract();

        Assert.That(CommitsOf(history, "Program.cs"), Is.EqualTo(new[] { "C" }));
        Assert.That(history.ChangeSets.SelectMany(cs => cs.Items).Select(item => item.ServerPath), Has.No.Member("Gone.cs"));
        Assert.That(history.ChangeSets.Select(cs => cs.Comment), Is.EqualTo(new[] { "C" }));
    }

    [Test]
    public void UnusualPaths_AreTracked()
    {
        const string oldName = "Ordner mit Leerzeichen/Datei äöü.cs";
        const string newName = "Neuer Ordner/Größe & 'Maß'.cs";

        _repo.Write(oldName, TestGitRepository.Content("umlauts"));
        _repo.Commit("A");
        _repo.Move(oldName, newName);
        _repo.Commit("B");

        var (history, warnings) = Extract();

        Assert.That(CommitsOf(history, newName), Is.EqualTo(new[] { "B", "A" }));
        Assert.That(warnings, Is.Empty);
    }

    [Test]
    public void ResultDoesNotDependOnTheGitConfiguration()
    {
        _repo.Write("Report.cs", TestGitRepository.Content("report"));
        _repo.Commit("A");

        _repo.CreateBranch("feature");
        _repo.Write("Report.cs", TestGitRepository.Content("report").Replace("line 3 of report", "line 3 changed on feature"));
        _repo.Commit("B");

        _repo.Checkout("main");
        _repo.Move("Report.cs", "Bericht für Größe.cs");
        _repo.Commit("C");

        _repo.Merge("feature", "M");

        // Settings that change what "git log" prints. The provider has to override all of them.
        // Set after the repository is built: diff.renames also changes how git merges.
        _repo.Git("config", "diff.renames", "false");
        _repo.Git("config", "log.diffMerges", "first-parent");
        _repo.Git("config", "log.showRoot", "false");
        _repo.Git("config", "core.quotepath", "true");
        _repo.Git("config", "color.ui", "always");

        var (history, warnings) = Extract();

        Assert.That(CommitsOf(history, "Bericht für Größe.cs"), Is.EquivalentTo(new[] { "M", "C", "B", "A" }));
        Assert.That(warnings, Is.Empty);
    }

    [Test]
    public void ExtractHistory_RejectsUncommittedChanges()
    {
        _repo.Write("a.txt", "one\n");
        _repo.Commit("A");
        _repo.Append("a.txt", "not committed\n");

        var provider = new GitProvider(_repo.Path);

        Assert.That(() => provider.ExtractHistory(null, false, new Filter()), Throws.ArgumentException);
    }
}
