using CodeParserTests.Helper;
using CSharpCodeAnalyst.CodeGraph.Algorithms.DeadCode;
using CSharpCodeAnalyst.CodeGraph.Declarations;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.DeadCode;

/// <summary>
///     The confidence of a finding follows three rules: a note about a caller outside the graph makes it
///     low, a direct finding confined to the analyzed code makes it high, everything else is medium.
/// </summary>
[TestFixture]
public class DeadCodeConfidenceTests
{
    [SetUp]
    public void SetUp()
    {
        _graph = new TestCodeGraph();
    }

    private TestCodeGraph _graph = null!;

    private void Rel(CodeElement source, CodeElement target, RelationshipType type)
    {
        source.Relationships.Add(new Relationship(source.Id, target.Id, type));
    }

    private DeadCodeConfidence ConfidenceOf(CodeElement element, ExternalContractStore? store = null)
    {
        return DeadCodeAnalysis.Calculate(_graph, store).Single(f => f.Element.Id == element.Id).Confidence;
    }

    /// <summary>A live class holding one used and one unused member - the unused one is the finding.</summary>
    private CodeElement CreateUnusedMember(AccessLevel memberAccess, AccessLevel classAccess = AccessLevel.Public)
    {
        var type = _graph.CreateClass("Widget", accessLevel: classAccess);
        var used = _graph.CreateMethod("Widget.Used", type, AccessLevel.Public);
        var unused = _graph.CreateMethod("Widget.Unused", type, memberAccess);

        var program = _graph.CreateClass("Program");
        var main = _graph.CreateMethod("Main", program);
        Rel(main, used, RelationshipType.Calls);

        return unused;
    }

    [Test]
    public void PrivateMember_IsHighConfidence()
    {
        // Nothing outside the type could call it, and nothing inside does.
        Assert.That(ConfidenceOf(CreateUnusedMember(AccessLevel.Private)), Is.EqualTo(DeadCodeConfidence.High));
    }

    [Test]
    public void InternalMember_IsHighConfidence()
    {
        Assert.That(ConfidenceOf(CreateUnusedMember(AccessLevel.Internal)), Is.EqualTo(DeadCodeConfidence.High));
    }

    [Test]
    public void PublicMember_IsMediumConfidence()
    {
        // A caller could sit in code we never analyzed.
        Assert.That(ConfidenceOf(CreateUnusedMember(AccessLevel.Public)), Is.EqualTo(DeadCodeConfidence.Medium));
    }

    [Test]
    public void ProtectedMember_IsMediumConfidence()
    {
        // A derived class in another assembly could override or call it.
        Assert.That(ConfidenceOf(CreateUnusedMember(AccessLevel.Protected)), Is.EqualTo(DeadCodeConfidence.Medium));
    }

    [Test]
    public void UnknownVisibility_IsMediumConfidence()
    {
        // What every importer other than the C# parser produces. "No information" must not be read as
        // "confined", so the finding cannot reach the top level.
        Assert.That(ConfidenceOf(CreateUnusedMember(AccessLevel.Unknown)), Is.EqualTo(DeadCodeConfidence.Medium));
    }

    [Test]
    public void PublicMemberOfAnInternalClass_IsHighConfidence()
    {
        // The effective reach is what counts: a public method of an internal class cannot be called from
        // another assembly either.
        var unused = CreateUnusedMember(AccessLevel.Public, AccessLevel.Internal);

        Assert.That(ConfidenceOf(unused), Is.EqualTo(DeadCodeConfidence.High));
    }

    [Test]
    public void NoteAboutACallerOutsideTheGraph_IsLowConfidence()
    {
        var type = _graph.CreateClass("Command", accessLevel: AccessLevel.Internal);
        var used = _graph.CreateMethod("Command.Used", type, AccessLevel.Private);
        var execute = _graph.CreateMethod("Command.Execute", type, AccessLevel.Private);

        var program = _graph.CreateClass("Program");
        var main = _graph.CreateMethod("Main", program);
        Rel(main, used, RelationshipType.Calls);

        var store = new ExternalContractStore();
        store.Add(execute.Id, "ICommand.Execute");

        // Private and internal would say "high", but we know the framework calls it - the note wins.
        Assert.That(ConfidenceOf(execute, store), Is.EqualTo(DeadCodeConfidence.Low));
    }

    [Test]
    public void StaticConstructor_IsAnEntryPointAndNotHighConfidence()
    {
        // The runtime runs it before the first use of the type; nothing in the code references it. It is
        // usually private, so without the entry point rule it would land in the highest confidence band.
        var type = _graph.CreateClass("Cache", accessLevel: AccessLevel.Internal);
        var staticConstructor = _graph.CreateMethod(".cctor", type, AccessLevel.Private);
        var used = _graph.CreateMethod("Cache.Used", type, AccessLevel.Public);

        var program = _graph.CreateClass("Program");
        var main = _graph.CreateMethod("Main", program);
        Rel(main, used, RelationshipType.Calls);

        var finding = DeadCodeAnalysis.Calculate(_graph).Single(f => f.Element.Id == staticConstructor.Id);

        Assert.Multiple(() =>
        {
            Assert.That(finding.Hints.HasFlag(DeadCodeHint.EntryPoint), Is.True);
            Assert.That(finding.Confidence, Is.EqualTo(DeadCodeConfidence.Low));
        });
    }

    [Test]
    public void CascadedFinding_IsNeverHighConfidence()
    {
        // Level 2 rests on level 1 being right, so it cannot be better than medium even when private.
        var report = _graph.CreateClass("Report", accessLevel: AccessLevel.Internal);
        var print = _graph.CreateMethod("Report.Print", report, AccessLevel.Private);
        var formatter = _graph.CreateClass("Formatter", accessLevel: AccessLevel.Internal);
        var format = _graph.CreateMethod("Formatter.Format", formatter, AccessLevel.Internal);
        Rel(print, format, RelationshipType.Calls);

        var findings = DeadCodeAnalysis.Calculate(_graph);
        var cascaded = findings.Single(f => f.Element.Id == formatter.Id);

        Assert.Multiple(() =>
        {
            Assert.That(cascaded.Level, Is.EqualTo(2));
            Assert.That(cascaded.Confidence, Is.EqualTo(DeadCodeConfidence.Medium));
            Assert.That(findings.Single(f => f.Element.Id == report.Id).Confidence,
                Is.EqualTo(DeadCodeConfidence.High));
        });
    }
}
