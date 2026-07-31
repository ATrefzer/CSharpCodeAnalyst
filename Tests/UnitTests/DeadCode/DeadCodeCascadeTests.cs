using CodeParserTests.Helper;
using CSharpCodeAnalyst.CodeGraph.Algorithms.DeadCode;
using CSharpCodeAnalyst.CodeGraph.Declarations;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.DeadCode;

/// <summary>
///     The cascade: what is only kept alive by dead code dies with it. The level says in which round a
///     finding appeared, and only findings without a note propagate - otherwise the class holding Main
///     would take the whole application down with it.
/// </summary>
[TestFixture]
public class DeadCodeCascadeTests
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

    private Dictionary<string, int> Levels(ExternalContractStore? store = null)
    {
        return DeadCodeAnalysis.Calculate(_graph, store)
            .ToDictionary(f => f.Element.FullName, f => f.Level);
    }

    [Test]
    public void Cascade_ChainOfUsers_DiesRoundByRound()
    {
        // Nothing references Report; Report is the only user of Formatter; Formatter the only user of Log.
        var report = _graph.CreateClass("Report");
        var print = _graph.CreateMethod("Report.Print", report);
        var formatter = _graph.CreateClass("Formatter");
        var format = _graph.CreateMethod("Formatter.Format", formatter);
        var log = _graph.CreateClass("Log");
        var write = _graph.CreateMethod("Log.Write", log);

        Rel(print, format, RelationshipType.Calls);
        Rel(format, write, RelationshipType.Calls);

        Assert.That(Levels(), Is.EquivalentTo(new Dictionary<string, int>
        {
            ["Report"] = 1,
            ["Formatter"] = 2,
            ["Log"] = 3
        }));
    }

    [Test]
    public void Cascade_LiveUser_KeepsTheChainAlive()
    {
        // Same chain, but something outside references Report - nothing dies.
        var report = _graph.CreateClass("Report");
        var print = _graph.CreateMethod("Report.Print", report);
        var formatter = _graph.CreateClass("Formatter");
        var format = _graph.CreateMethod("Formatter.Format", formatter);

        var program = _graph.CreateClass("Program");
        var main = _graph.CreateMethod("Main", program);
        Rel(main, print, RelationshipType.Calls);
        Rel(print, format, RelationshipType.Calls);

        // Program is reported (nothing references it) but carries the entry point note, so it does not
        // propagate - everything it reaches stays alive.
        Assert.That(Levels(), Is.EquivalentTo(new Dictionary<string, int> { ["Program"] = 1 }));
    }

    [Test]
    public void Cascade_EntryPoint_DoesNotTakeTheApplicationDownWithIt()
    {
        var program = _graph.CreateClass("Program");
        var main = _graph.CreateMethod("Main", program);
        var service = _graph.CreateClass("Service");
        var run = _graph.CreateMethod("Service.Run", service);
        Rel(main, run, RelationshipType.Calls);

        var findings = DeadCodeAnalysis.Calculate(_graph);

        Assert.Multiple(() =>
        {
            Assert.That(findings.Select(f => f.Element.FullName), Is.EquivalentTo(new[] { "Program" }));
            Assert.That(findings.Single().Hints.HasFlag(DeadCodeHint.EntryPoint), Is.True);
        });
    }

    [Test]
    public void Cascade_TestFixture_DoesNotPropagate()
    {
        var fixture = _graph.CreateClass("MyTests");
        var testMethod = _graph.CreateMethod("MyTests.ShouldWork", fixture);
        testMethod.Attributes.Add("TestAttribute");

        var subject = _graph.CreateClass("Subject");
        var doWork = _graph.CreateMethod("Subject.DoWork", subject);
        Rel(testMethod, doWork, RelationshipType.Calls);

        Assert.That(Levels(), Is.EquivalentTo(new Dictionary<string, int> { ["MyTests"] = 1 }));
    }

    [Test]
    public void Cascade_ExternalContractImplementation_DoesNotPropagate()
    {
        // Execute is called by the framework, so what it calls is alive even though Execute is reported.
        var command = _graph.CreateClass("Command");
        var execute = _graph.CreateMethod("Command.Execute", command);
        var service = _graph.CreateClass("Service");
        var run = _graph.CreateMethod("Service.Run", service);
        Rel(execute, run, RelationshipType.Calls);

        var store = new ExternalContractStore();
        store.Add(execute.Id, "ICommand.Execute");

        // Command is dead and swallows Execute by roll-up, so the note is not on the reported row - but
        // the subtree still holds a member the framework calls, so nothing may be derived from it.
        Assert.That(Levels(store), Is.EquivalentTo(new Dictionary<string, int> { ["Command"] = 1 }));
    }

    [Test]
    public void Cascade_AttributedElement_DoesNotPropagate()
    {
        // An attribute often means a framework drives the element, so it is not evidence of death.
        var driven = _graph.CreateClass("Driven");
        driven.Attributes.Add("SerializableAttribute");
        var method = _graph.CreateMethod("Driven.M", driven);
        var helper = _graph.CreateClass("Helper");
        var help = _graph.CreateMethod("Helper.Help", helper);
        Rel(method, help, RelationshipType.Calls);

        Assert.That(Levels(), Is.EquivalentTo(new Dictionary<string, int> { ["Driven"] = 1 }));
    }

    [Test]
    public void Cascade_MutualReference_IsNotFound()
    {
        // The known limit: two elements that only reference each other keep each other alive. Finding
        // those needs reachability from an explicit set of entry points, not a cascade.
        var a = _graph.CreateClass("A");
        var am = _graph.CreateMethod("A.M", a);
        var b = _graph.CreateClass("B");
        var bm = _graph.CreateMethod("B.M", b);
        Rel(am, bm, RelationshipType.Calls);
        Rel(bm, am, RelationshipType.Calls);

        Assert.That(Levels(), Is.Empty);
    }

    [Test]
    public void Cascade_MemberOfALiveClass_DiesWithItsOnlyCaller()
    {
        // Widget stays alive, but Unused is only called from the dead Report.
        var widget = _graph.CreateClass("Widget");
        var used = _graph.CreateMethod("Widget.Used", widget);
        var unused = _graph.CreateMethod("Widget.Unused", widget);

        var program = _graph.CreateClass("Program");
        var main = _graph.CreateMethod("Main", program);
        Rel(main, used, RelationshipType.Calls);

        var report = _graph.CreateClass("Report");
        var print = _graph.CreateMethod("Report.Print", report);
        Rel(print, unused, RelationshipType.Calls);

        Assert.That(Levels(), Is.EquivalentTo(new Dictionary<string, int>
        {
            ["Program"] = 1,
            ["Report"] = 1,
            ["Widget.Unused"] = 2
        }));
    }
}
