using CodeParserTests.Helper;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeGraph.Metrics;

namespace CodeParserTests.UnitTests.ArchitecturalRules;

/// <summary>
///     Covers the assembly-level rule generation. The generated rules must freeze the current
///     dependency structure and therefore validate clean against the same graph.
/// </summary>
[TestFixture]
public class AssemblyRuleGeneratorTests
{

    [SetUp]
    public void SetUp()
    {
        _codeGraph = new TestCodeGraph();
    }

    private TestCodeGraph _codeGraph;

    /// <summary>Adds a class in <paramref name="assembly" /> and returns it.</summary>
    private CodeElement ClassIn(CodeElement assembly, string name)
    {
        return _codeGraph.CreateClass(name, assembly);
    }

    private static void Depend(CodeElement from, CodeElement to)
    {
        from.Relationships.Add(new Relationship(from.Id, to.Id, RelationshipType.Uses));
    }

    private List<string> GenerateLines()
    {
        return AssemblyRuleGenerator.Generate(_codeGraph)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .ToList();
    }

    [Test]
    public void SingleDependency_ProducesRestrict_AndIsolateForTheSink()
    {
        var a = _codeGraph.CreateAssembly("AsmA");
        var b = _codeGraph.CreateAssembly("AsmB");
        Depend(ClassIn(a, "A1"), ClassIn(b, "B1"));

        var lines = GenerateLines();

        Assert.That(lines, Does.Contain("RESTRICT: AsmA.** -> AsmB.**"));
        Assert.That(lines, Does.Contain("ISOLATE: AsmB.**"));
        Assert.That(lines, Has.Count.EqualTo(2));
    }

    [Test]
    public void MultipleDependencies_FreezeAsRestrictToEachCurrentTarget()
    {
        // A -> B, A -> C : A may only depend on exactly B and C.
        var a = _codeGraph.CreateAssembly("AsmA");
        var b = _codeGraph.CreateAssembly("AsmB");
        var c = _codeGraph.CreateAssembly("AsmC");

        Depend(ClassIn(a, "A1"), ClassIn(b, "B1"));
        Depend(ClassIn(a, "A2"), ClassIn(c, "C1"));

        var lines = GenerateLines();

        Assert.That(lines, Does.Contain("RESTRICT: AsmA.** -> AsmB.**"));
        Assert.That(lines, Does.Contain("RESTRICT: AsmA.** -> AsmC.**"));
        Assert.That(lines, Does.Contain("ISOLATE: AsmB.**"));
        Assert.That(lines, Does.Contain("ISOLATE: AsmC.**"));
        // No DENY / no implicit assumptions - just the current state.
        Assert.That(lines, Has.None.StartsWith("DENY"));
    }

    [Test]
    public void MutualDependency_FrozenAsRestrictInBothDirections()
    {
        // A <-> B cycle: the current state (including the cycle) is captured verbatim.
        var a = _codeGraph.CreateAssembly("AsmA");
        var b = _codeGraph.CreateAssembly("AsmB");

        Depend(ClassIn(a, "A1"), ClassIn(b, "B1"));
        Depend(ClassIn(b, "B2"), ClassIn(a, "A2"));

        var lines = GenerateLines();

        Assert.That(lines, Does.Contain("RESTRICT: AsmA.** -> AsmB.**"));
        Assert.That(lines, Does.Contain("RESTRICT: AsmB.** -> AsmA.**"));
        Assert.That(lines, Has.Count.EqualTo(2));
    }

    [Test]
    public void NewDependencyOutsideTheFrozenSet_IsReported()
    {
        // Freeze A -> B, then A gains a dependency on C: it must be flagged.
        var a = _codeGraph.CreateAssembly("AsmA");
        var b = _codeGraph.CreateAssembly("AsmB");
        var c = _codeGraph.CreateAssembly("AsmC");
        Depend(ClassIn(a, "A1"), ClassIn(b, "B1"));

        var generated = AssemblyRuleGenerator.Generate(_codeGraph);

        // New, not-yet-existing dependency.
        Depend(ClassIn(a, "A2"), ClassIn(c, "C1"));

        var result = RuleEngine.Execute(RuleParser.ParseRules(generated), _codeGraph, new MetricStore());

        Assert.That(result.Violations, Has.Count.EqualTo(1), "the new A -> C dependency must be reported");
    }

    [Test]
    public void GeneratedRules_ValidateCleanAgainstTheSameGraph()
    {
        // Strong invariant: a generated rule set is a clean baseline of the current structure.
        var a = _codeGraph.CreateAssembly("AsmA");
        var b = _codeGraph.CreateAssembly("AsmB");
        var c = _codeGraph.CreateAssembly("AsmC");
        var d = _codeGraph.CreateAssembly("AsmD");

        Depend(ClassIn(a, "A1"), ClassIn(b, "B1"));
        Depend(ClassIn(a, "A2"), ClassIn(c, "C1"));
        Depend(ClassIn(d, "D1"), ClassIn(a, "A3"));
        Depend(ClassIn(b, "B2"), ClassIn(c, "C2"));

        var generated = AssemblyRuleGenerator.Generate(_codeGraph);
        var result = RuleEngine.Execute(RuleParser.ParseRules(generated), _codeGraph, new MetricStore());

        Assert.That(result.Violations, Is.Empty, "generated rules must not flag the current structure");
        Assert.That(result.Warnings, Is.Empty, "every generated pattern must match an assembly");
    }

    [Test]
    public void ExternalAssemblies_AreIgnored()
    {
        var a = _codeGraph.CreateAssembly("AsmA");
        var external = new CodeElement("Ext", CodeElementType.Assembly, "System", "System", null)
        {
            IsExternal = true
        };
        _codeGraph.Nodes["Ext"] = external;
        var externalType = new CodeElement("ExtType", CodeElementType.Class, "String", "System.String", external);
        _codeGraph.Nodes["ExtType"] = externalType;
        external.Children.Add(externalType);

        Depend(ClassIn(a, "A1"), externalType);

        var lines = GenerateLines();

        // A only depends on external code -> treated as no internal dependency -> ISOLATE.
        Assert.That(lines, Does.Contain("ISOLATE: AsmA.**"));
        Assert.That(lines, Has.None.Contains("System"));
    }

    [Test]
    public void NoAssemblies_ProducesEmptyOutput()
    {
        Assert.That(AssemblyRuleGenerator.Generate(_codeGraph), Is.Empty);
    }
}
