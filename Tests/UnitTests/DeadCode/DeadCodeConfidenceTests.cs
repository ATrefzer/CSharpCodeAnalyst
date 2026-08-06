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

    /// <summary>
    ///     A view model with one used and one unused property. The recorded notifying type is what tells
    ///     the analysis that a {Binding} may read the public properties. Where the interface is
    ///     implemented - own code, an internal base, an external base like ObservableObject - makes no
    ///     difference here; that distinction lives in the parser (see NotifyingTypeParseTests).
    /// </summary>
    private (CodeElement Unused, ExternalContractStore Store) CreateViewModel(AccessLevel propertyAccess,
        AccessLevel typeAccess = AccessLevel.Internal)
    {
        var viewModel = _graph.CreateClass("MainViewModel", accessLevel: typeAccess);
        var used = _graph.CreateMethod("MainViewModel.Used", viewModel, AccessLevel.Public);
        var unused = _graph.CreateProperty("MainViewModel.Title", viewModel);
        unused = Retype(unused, propertyAccess);

        var program = _graph.CreateClass("Program");
        var main = _graph.CreateMethod("Main", program);
        Rel(main, used, RelationshipType.Calls);

        var store = new ExternalContractStore();
        store.AddNotifyingType(viewModel.Id);
        return (unused, store);
    }

    /// <summary>TestCodeGraph has no accessibility overload for properties - replace the element.</summary>
    private CodeElement Retype(CodeElement element, AccessLevel accessLevel)
    {
        var replacement = new CodeElement(element.Id, element.ElementType, element.Name, element.FullName,
            element.Parent) { AccessLevel = accessLevel };
        element.Parent?.Children.Remove(element);
        element.Parent?.Children.Add(replacement);
        _graph.Nodes[replacement.Id] = replacement;
        return replacement;
    }

    [Test]
    public void PublicPropertyOnANotifyingType_IsNotHighConfidence()
    {
        // A XAML {Binding} reaches exactly this and is invisible to the analysis.
        var (unused, store) = CreateViewModel(AccessLevel.Public);

        Assert.That(ConfidenceOf(unused, store), Is.EqualTo(DeadCodeConfidence.Medium));
    }

    [Test]
    public void PrivatePropertyOnANotifyingType_StaysHighConfidence()
    {
        // The binding engine resolves by public reflection, so it can never reach this one.
        var (unused, store) = CreateViewModel(AccessLevel.Private);

        Assert.That(ConfidenceOf(unused, store), Is.EqualTo(DeadCodeConfidence.High));
    }

    [Test]
    public void PublicPropertyOnAnOrdinaryType_StaysHighConfidence()
    {
        // Without the notification contract there is no reason to suspect a binding.
        var type = _graph.CreateClass("Options", accessLevel: AccessLevel.Internal);
        var used = _graph.CreateMethod("Options.Used", type, AccessLevel.Public);
        var unused = Retype(_graph.CreateProperty("Options.Title", type), AccessLevel.Public);

        var program = _graph.CreateClass("Program");
        var main = _graph.CreateMethod("Main", program);
        Rel(main, used, RelationshipType.Calls);

        Assert.That(ConfidenceOf(unused), Is.EqualTo(DeadCodeConfidence.High));
    }

}
