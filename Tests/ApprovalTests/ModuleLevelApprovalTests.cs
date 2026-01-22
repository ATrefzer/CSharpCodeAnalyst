using CodeGraph.Graph;

namespace CodeParserTests.ApprovalTests;

[TestFixture]
public class ModuleLevelApprovalTests : ApprovalTestBase
{

    private CodeGraph.Graph.CodeGraph GetTestAssemblyGraph()
    {
        var g0 = GetTestGraph("ModuleLevel0");
        var g1 = GetTestGraph("ModuleLevel1");
        var g2 = GetTestGraph("ModuleLevel2");

        return Graph.SubGraphOf(g0.Nodes.Keys.Union(g1.Nodes.Keys).Union(g2.Nodes.Keys).ToHashSet());
    }

    [Test]
    public void Classes_ShouldBeDetected()
    {
        var actual = GetAllClasses(GetTestAssemblyGraph());

        var expected = new[]
        {
            "ModuleLevel0.global.ModuleLevel0.Ns1.ClassL",
            "ModuleLevel0.global.ModuleLevel0.Ns1.ClassL.InnerClassL",
            "ModuleLevel0.global.ModuleLevel0.Ns1.Ns2.ClassY",
            "ModuleLevel1.global.ModuleLevel1.FactoryC",
            "ModuleLevel1.global.ModuleLevel1.Model.ModelA",
            "ModuleLevel1.global.ModuleLevel1.Model.ModelB",
            "ModuleLevel1.global.ModuleLevel1.Model.ModelC",
            "ModuleLevel1.global.ModuleLevel1.Model.ModelD",
            "ModuleLevel0.global.ModuleLevel0.Bootstrapper",

            "ModuleLevel1.global.ModuleLevel1.Command",
            "ModuleLevel1.global.ModuleLevel1.ServiceA",
            "ModuleLevel1.global.ModuleLevel1.ServiceBase",
            "ModuleLevel1.global.ModuleLevel1.ServiceC",

            "ModuleLevel2.global.ModuleLevel2.Constants",
            "ModuleLevel2.global.ModuleLevel2.DerivedFromGenericSystemClass",
            "ModuleLevel2.global.ModuleLevel2.N1.ClassInNs1",
            "ModuleLevel2.global.ModuleLevel2.N1.N2.N3.ClassInNs2",
            "ModuleLevel2.global.ModuleLevel2.SelfReferencingClass",
            "ModuleLevel2.global.ModuleLevel2.Utility",

            "ModuleLevel0.global.ModuleLevel2.InterfaceImplementerInDifferentCompilation",
            "ModuleLevel2.global.ClassInGlobalNs",
            "ModuleLevel2.global.Insight.Analyzers",
            "ModuleLevel2.global.Insight.Dialogs.TrendViewModel"
        };

        Assert.That(actual, Is.EquivalentTo(expected));
    }

    [Test]
    public void Properties_ShouldBeDetected()
    {
        var actual = GetAllNodesOfType(GetTestAssemblyGraph(), CodeElementType.Property);

        var expected = new HashSet<string>
        {
            "ModuleLevel1.global.ModuleLevel1.IServiceC.IfProperty",
            "ModuleLevel1.global.ModuleLevel1.Model.ModelA.ModelCPropertyOfModelA",
            "ModuleLevel1.global.ModuleLevel1.Model.ModelB.Value",
            "ModuleLevel1.global.ModuleLevel1.Model.ModelC.IntPropertyOfModelC",
            "ModuleLevel1.global.ModuleLevel1.Model.StructA.DependencyToConstant",
            "ModuleLevel1.global.ModuleLevel1.ServiceBase.IfProperty",
            "ModuleLevel1.global.ModuleLevel1.ServiceC.IfProperty",
            "ModuleLevel2.global.ModuleLevel2.SelfReferencingClass.Commit",
            "ModuleLevel2.global.ModuleLevel2.SelfReferencingClass.CommitHash",
            "ModuleLevel2.global.ModuleLevel2.SelfReferencingClass.Parents",
            "ModuleLevel2.global.ModuleLevel2.SelfReferencingClass.Children"
        };


        Assert.That(actual, Is.EquivalentTo(expected));
    }

    private static CodeElement GetAssembly(CodeElement element)
    {
        var assembly = element;
        while (assembly != null && assembly.ElementType != CodeElementType.Assembly)
        {
            assembly = assembly.Parent;
        }

        Assert.That(assembly != null);
        return assembly!;
    }

    [Test]
    public void CrossProjectUsages_ShouldBeDetected()
    {
        var graph = GetTestAssemblyGraph();
        var crossing = graph.GetAllRelationships()
            .Where(r => GetAssembly(graph.Nodes[r.SourceId]).Id != GetAssembly(graph.Nodes[r.TargetId]).Id)
            .Select(CreateResolvedRelationShip)
            .ToList();


        var actual = crossing
            .Select(r => $"{r.Source} -> {r.Target}")
            .ToHashSet();

        var expected = new[]
        {
            "ModuleLevel0.global.ModuleLevel0.Bootstrapper.Run -> ModuleLevel1.global.ModuleLevel1.FactoryC",
            "ModuleLevel0.global.ModuleLevel0.Bootstrapper.Run -> ModuleLevel1.global.ModuleLevel1.FactoryC.Create",
            "ModuleLevel0.global.ModuleLevel0.Bootstrapper.Run -> ModuleLevel1.global.ModuleLevel1.IServiceC.Do",
            "ModuleLevel0.global.ModuleLevel0.Bootstrapper.Run -> ModuleLevel2.global.ModuleLevel2.Constants.Constant1",
            "ModuleLevel0.global.ModuleLevel2.InterfaceImplementerInDifferentCompilation -> ModuleLevel2.global.ModuleLevel0.InterfaceInDifferentCompilation",
            "ModuleLevel0.global.ModuleLevel2.InterfaceImplementerInDifferentCompilation.AEvent -> ModuleLevel2.global.ModuleLevel0.InterfaceInDifferentCompilation.AEvent",
            "ModuleLevel0.global.ModuleLevel2.InterfaceImplementerInDifferentCompilation.Method -> ModuleLevel2.global.ModuleLevel0.InterfaceInDifferentCompilation.Method",
            "ModuleLevel1.global.ModuleLevel1.Model.ModelB.Initialize -> ModuleLevel2.global.ModuleLevel2.TheEnum",
            "ModuleLevel1.global.ModuleLevel1.Model.ModelB.Do -> ModuleLevel2.global.ModuleLevel2.TheEnum",
            "ModuleLevel1.global.ModuleLevel1.Model.ModelC.MethodOnModelC -> ModuleLevel2.global.ModuleLevel2.TheEnum",
            "ModuleLevel1.global.ModuleLevel1.Model.ModelC.MethodOnModelCCalledFromLambda -> ModuleLevel2.global.ModuleLevel2.TheEnum",
            "ModuleLevel1.global.ModuleLevel1.Model.StructA.DependencyToConstant -> ModuleLevel2.global.ModuleLevel2.Constants.Constant1",
            "ModuleLevel1.global.ModuleLevel1.ServiceC.Do -> ModuleLevel2.global.ModuleLevel2.Utility.UtilityMethod1",

            // LocalDeclarationSyntax
            "ModuleLevel0.global.ModuleLevel0.Bootstrapper.Run -> ModuleLevel1.global.ModuleLevel1.IServiceC"
        };

        Assert.That(actual, Is.EquivalentTo(expected));
    }

    [Test]
    public void FindsAllPropertyImplementations()
    {
        var graph = GetTestAssemblyGraph();

        // Realize an interface
        var actual = GetAllPropertyImplementations(graph);
        var expected = new HashSet<string>
        {
            "ModuleLevel1.global.ModuleLevel1.ServiceBase.IfProperty -> ModuleLevel1.global.ModuleLevel1.IServiceC.IfProperty"
        };


        Assert.That(actual, Is.EquivalentTo(expected));
    }

    [Test]
    public void FindsAllEventImplementations()
    {
        var graph = GetTestAssemblyGraph();
        var actual = GetAllEventImplementations(graph);


        var expected = new HashSet<string>
        {
            "ModuleLevel0.global.ModuleLevel2.InterfaceImplementerInDifferentCompilation.AEvent -> ModuleLevel2.global.ModuleLevel0.InterfaceInDifferentCompilation.AEvent"
        };


        Assert.That(actual, Is.EquivalentTo(expected));
    }

    [Test]
    public void FindsAllPropertyOverrides()
    {
        var graph = GetTestAssemblyGraph();
        var actual = GetAllPropertyOverrides(graph);

        var expected = new HashSet<string>
        {
            "ModuleLevel1.global.ModuleLevel1.ServiceC.IfProperty -> ModuleLevel1.global.ModuleLevel1.ServiceBase.IfProperty"
        };


        Assert.That(actual, Is.EquivalentTo(expected));
    }
}