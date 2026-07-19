using CodeParserTests.Helper;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.ArchitecturalRules;

/// <summary>
///     The parser inserts a synthetic "global" namespace below every assembly as soon as ANY assembly
///     in the solution holds code outside a namespace. Whether a full name reads "MyApp.Business" or
///     "MyApp.global.Business" therefore depends on a property of a completely different project, and
///     it flips when a top-level-statement project is added or removed. Rule files must survive that:
///     both spellings resolve to the same element.
/// </summary>
[TestFixture]
public class PatternMatcherGlobalNamespaceTests
{
    /// <summary>Graph shape as the parser builds it when a global namespace is needed.</summary>
    private static TestCodeGraph GraphWithGlobalNamespace()
    {
        var graph = new TestCodeGraph();
        var assembly = graph.CreateAssembly("MyApp");
        var globalNs = graph.CreateNamespace("MyApp.global", assembly);
        var business = graph.CreateNamespace("MyApp.global.Business", globalNs);
        graph.CreateClass("MyApp.global.Business.OrderService", business);
        return graph;
    }

    /// <summary>Same code, but no assembly in the solution needed the global namespace.</summary>
    private static TestCodeGraph GraphWithoutGlobalNamespace()
    {
        var graph = new TestCodeGraph();
        var assembly = graph.CreateAssembly("MyApp");
        var business = graph.CreateNamespace("MyApp.Business", assembly);
        graph.CreateClass("MyApp.Business.OrderService", business);
        return graph;
    }

    [Test]
    public void PatternWithoutGlobal_MatchesGraphWithGlobal()
    {
        // The user omits the synthetic segment - the rule must still find the namespace.
        var graph = GraphWithGlobalNamespace();

        var result = PatternMatcher.ResolvePattern("MyApp.Business.**", graph);

        Assert.That(result, Is.EquivalentTo(new[] { "MyApp.global.Business", "MyApp.global.Business.OrderService" }));
    }

    [Test]
    public void PatternWithGlobal_MatchesGraphWithGlobal()
    {
        // Spelling it out must keep working - that is what the rule generators emit.
        var graph = GraphWithGlobalNamespace();

        var result = PatternMatcher.ResolvePattern("MyApp.global.Business.**", graph);

        Assert.That(result, Is.EquivalentTo(new[] { "MyApp.global.Business", "MyApp.global.Business.OrderService" }));
    }

    [Test]
    public void PatternWithGlobal_MatchesGraphWithoutGlobal()
    {
        // The other direction: an existing rule file (or baseline) that spells out "global" must not
        // break when the last top-level-statement project disappears from the solution.
        var graph = GraphWithoutGlobalNamespace();

        var result = PatternMatcher.ResolvePattern("MyApp.global.Business.**", graph);

        Assert.That(result, Is.EquivalentTo(new[] { "MyApp.Business", "MyApp.Business.OrderService" }));
    }

    [Test]
    public void PatternWithoutGlobal_MatchesGraphWithoutGlobal()
    {
        var graph = GraphWithoutGlobalNamespace();

        var result = PatternMatcher.ResolvePattern("MyApp.Business.**", graph);

        Assert.That(result, Is.EquivalentTo(new[] { "MyApp.Business", "MyApp.Business.OrderService" }));
    }

    [Test]
    public void ElementDeeperInHierarchy_ResolvesWithoutGlobal()
    {
        var graph = GraphWithGlobalNamespace();

        var result = PatternMatcher.ResolvePattern("MyApp.Business.OrderService", graph);

        Assert.That(result, Is.EquivalentTo(new[] { "MyApp.global.Business.OrderService" }));
    }

    [Test]
    public void DottedAssemblyName_IsHandled()
    {
        // The assembly prefix is taken from the graph, not by splitting off the first segment -
        // assembly names contain dots themselves.
        var graph = new TestCodeGraph();
        var assembly = graph.CreateAssembly("MyApp.Business.Core");
        var globalNs = graph.CreateNamespace("MyApp.Business.Core.global", assembly);
        graph.CreateClass("MyApp.Business.Core.global.OrderService", globalNs);

        var result = PatternMatcher.ResolvePattern("MyApp.Business.Core.OrderService", graph);

        Assert.That(result, Is.EquivalentTo(new[] { "MyApp.Business.Core.global.OrderService" }));
    }

    [Test]
    public void GlobalNamespaceItself_IsAddressable()
    {
        // "MyApp.global" names the namespace node; the assembly stays a different element.
        var graph = GraphWithGlobalNamespace();

        var globalResult = PatternMatcher.ResolvePattern("MyApp.global", graph);
        var assemblyResult = PatternMatcher.ResolvePattern("MyApp", graph);

        Assert.Multiple(() =>
        {
            Assert.That(globalResult, Is.EquivalentTo(new[] { "MyApp.global" }));
            Assert.That(assemblyResult, Is.EquivalentTo(new[] { "MyApp" }));
        });
    }

    [Test]
    public void GlobalNamespaceInRule_FallsBackToAssembly_WhenGraphHasNone()
    {
        // Without a global namespace the assembly is the container that plays its role.
        var graph = GraphWithoutGlobalNamespace();

        var result = PatternMatcher.ResolvePattern("MyApp.global", graph);

        Assert.That(result, Is.EquivalentTo(new[] { "MyApp" }));
    }

    [Test]
    public void ExactMatchWins_NoOverMatching()
    {
        // A namespace really named "global" one level down must not be confused with the synthetic
        // one directly below the assembly: the exact match is taken and the fallback never runs.
        var graph = new TestCodeGraph();
        var assembly = graph.CreateAssembly("MyApp");
        var globalNs = graph.CreateNamespace("MyApp.global", assembly);
        var business = graph.CreateNamespace("MyApp.global.Business", globalNs);
        graph.CreateClass("MyApp.global.Business.OrderService", business);

        // Both spellings exist as elements - the written one must win.
        graph.CreateNamespace("MyApp.Business", assembly);

        var result = PatternMatcher.ResolvePattern("MyApp.Business", graph);

        Assert.That(result, Is.EquivalentTo(new[] { "MyApp.Business" }));
    }

    [Test]
    public void UnknownAssembly_StillYieldsNoMatch()
    {
        // The fallback must not turn a typo into a match - the no-match warning has to keep firing.
        var graph = GraphWithGlobalNamespace();

        var result = PatternMatcher.ResolvePattern("Unknown.Business.**", graph);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void CaseMismatch_StillYieldsNoMatch()
    {
        var graph = GraphWithGlobalNamespace();

        var result = PatternMatcher.ResolvePattern("MyApp.business.**", graph);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void ResolveSubtree_ToleratesOmittedGlobal()
    {
        // NOCYCLES uses the same anchor resolution.
        var graph = GraphWithGlobalNamespace();

        var result = PatternMatcher.ResolveSubtree("MyApp.Business", graph);

        Assert.That(result, Is.EquivalentTo(new[] { "MyApp.global.Business", "MyApp.global.Business.OrderService" }));
    }
}
