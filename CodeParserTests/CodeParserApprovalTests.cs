using CodeParser.Analysis.Cycles;
using CodeParser.Parser;
using CodeParser.Parser.Config;
using Contracts.Graph;

namespace CodeParserTests;

public class CodeParserApprovalTests
{
    private CodeGraph _graph;

    [OneTimeSetUp]
    public async Task FixtureSetup()
    {
        Initializer.InitializeMsBuildLocator();
        var parser = new Parser(new ParserConfig(new ProjectExclusionRegExCollection()));
        _graph = await parser.ParseSolution(@"..\..\..\..\SampleProject\SampleProject.sln");
    }


    [Test]
    public void FindsAllAssemblies()
    {
        var codeElements = _graph.Nodes.Values;

        // Three assemblies
        var assemblies = codeElements.Where(n => n is { Name: "ModuleLevel0", ElementType: CodeElementType.Assembly });
        Assert.AreEqual(1, assemblies.Count());

        assemblies = codeElements.Where(n => n is { Name: "ModuleLevel1", ElementType: CodeElementType.Assembly });
        Assert.AreEqual(1, assemblies.Count());

        assemblies = codeElements.Where(n => n is { Name: "ModuleLevel2", ElementType: CodeElementType.Assembly });
        Assert.AreEqual(1, assemblies.Count());

        assemblies = codeElements.Where(n => n is { Name: "CSharpLanguage", ElementType: CodeElementType.Assembly });
        Assert.AreEqual(1, assemblies.Count());

        assemblies = codeElements.Where(n => n is { Name: "Cycles", ElementType: CodeElementType.Assembly });
        Assert.AreEqual(1, assemblies.Count());

        assemblies = codeElements.Where(n => n.ElementType == CodeElementType.Assembly);
        Assert.AreEqual(5, assemblies.Count());
    }

    [Test]
    public void FindsAllFunctions()
    {
        var codeElements = _graph.Nodes.Values;

        var methods = codeElements
            .Where(n => n.ElementType == CodeElementType.Method)
            .Select(m => m.FullName).ToList();


        // No HashSet because some methods are overloaded
        var expectedMethods = new List<string>
        {
            "ModuleLevel0.ModuleLevel0.Bootstrapper.Run", "ModuleLevel1.ModuleLevel1.FactoryC.Create",
            "ModuleLevel1.ModuleLevel1.IServiceC.Do", "ModuleLevel1.ModuleLevel1.Model.ModelA..ctor",
            "ModuleLevel1.ModuleLevel1.Model.ModelA.GetModelC", "ModuleLevel1.ModuleLevel1.Model.ModelA.GetModelD",
            "ModuleLevel1.ModuleLevel1.Model.ModelB.Initialize", "ModuleLevel1.ModuleLevel1.Model.ModelB.Do",
            "ModuleLevel1.ModuleLevel1.Model.ModelC.RecursiveFuncOnModelC",
            "ModuleLevel1.ModuleLevel1.Model.ModelC.MethodOnModelC",
            "ModuleLevel1.ModuleLevel1.Model.ModelC.MethodOnModelCCalledFromLambda",
            "ModuleLevel1.ModuleLevel1.Model.StructA..ctor", "ModuleLevel1.ModuleLevel1.Model.StructA.Fill",
            "ModuleLevel1.ModuleLevel1.ServiceBase.Do", "ModuleLevel1.ModuleLevel1.ServiceC.Do",
            "ModuleLevel1.ModuleLevel1.ServiceC.Execute", "ModuleLevel2.ModuleLevel2.SelfReferencingClass..ctor",
            "ModuleLevel2.ModuleLevel2.Utility.UtilityMethod1", "ModuleLevel2.ModuleLevel2.Utility.UtilityMethod2",
            "CSharpLanguage.CSharpLanguage.ClassOfferingAnEvent.OnEvent",
            "CSharpLanguage.CSharpLanguage.ClassUsingAnEvent.Init",
            "CSharpLanguage.CSharpLanguage.ClassUsingAnEvent.MyEventHandler2",
            "CSharpLanguage.CSharpLanguage.ClassUsingAnEvent.MyEventHandler",
            "CSharpLanguage.CSharpLanguage.CreatorOfGenericTypes.Create",
            "CSharpLanguage.CSharpLanguage.PinSignalView.OnCreteAutomationPeer",
            "CSharpLanguage.CSharpLanguage.PinSignalView.PinSignalViewAutomationPeer..ctor",
            "CSharpLanguage.CSharpLanguage.MissingInterface.BaseStorage.Load",
            "CSharpLanguage.CSharpLanguage.MissingInterface.IStorage.Load",
            "CSharpLanguage.CSharpLanguage.NS_Parent.NS_Child.ClassNsChild.Method",

            "CSharpLanguage.CSharpLanguage.IStructInterface.Method",
            "CSharpLanguage.CSharpLanguage.StructWithInterface.Method",

            // Extension method
            "CSharpLanguage.CSharpLanguage.Extensions.Slice",

            // Called by extension method
            "CSharpLanguage.CSharpLanguage.TheExtendedType.Do",

            // Generic method calls
            "CSharpLanguage.CSharpLanguage.MoreGenerics.M1",
            "CSharpLanguage.CSharpLanguage.MoreGenerics.M1",
            "CSharpLanguage.CSharpLanguage.MoreGenerics.M2",
            "CSharpLanguage.CSharpLanguage.MoreGenerics.M2",
            "CSharpLanguage.CSharpLanguage.MoreGenerics.Run",

            "ModuleLevel1.ModuleLevel1.Model.ModelA.AccessToPropertiesGetter",
            "ModuleLevel1.ModuleLevel1.Model.ModelA.AccessToPropertiesSetter",

            // Show event registration
            "CSharpLanguage.CSharpLanguage.EventInvocation.DoSomething", 
            "CSharpLanguage.CSharpLanguage.EventInvocation.Raise1", 
            "CSharpLanguage.CSharpLanguage.EventInvocation.Raise2",
            "CSharpLanguage.CSharpLanguage.EventInvocation.Raise3",
            "CSharpLanguage.CSharpLanguage.EventSink..ctor", 
            "CSharpLanguage.CSharpLanguage.EventSink.Handler"
        };


        CollectionAssert.AreEquivalent(expectedMethods, methods);
    }

    [Test]
    public void FindsAllCalls()
    {
        var calls = _graph.GetAllDependencies().Where(d => d.Type == DependencyType.Calls);

        var actual = calls.Select(d => $"{_graph.Nodes[d.SourceId].FullName} -> {_graph.Nodes[d.TargetId].FullName}")
            .ToHashSet();

        var expected = new HashSet<string>
        {
            "CSharpLanguage.CSharpLanguage.ClassUsingAnEvent.Init -> CSharpLanguage.CSharpLanguage.Extensions.Slice",
            "CSharpLanguage.CSharpLanguage.Extensions.Slice -> CSharpLanguage.CSharpLanguage.TheExtendedType.Do",
            "CSharpLanguage.CSharpLanguage.MoreGenerics.Run -> CSharpLanguage.CSharpLanguage.MoreGenerics.M1",
            "CSharpLanguage.CSharpLanguage.MoreGenerics.Run -> CSharpLanguage.CSharpLanguage.MoreGenerics.M2",
            "ModuleLevel0.ModuleLevel0.Bootstrapper.Run -> ModuleLevel1.ModuleLevel1.FactoryC.Create",
            "ModuleLevel0.ModuleLevel0.Bootstrapper.Run -> ModuleLevel1.ModuleLevel1.IServiceC.Do",
            "ModuleLevel1.ModuleLevel1.Model.ModelA..ctor -> ModuleLevel1.ModuleLevel1.Model.ModelB.Initialize",
            "ModuleLevel1.ModuleLevel1.Model.ModelA..ctor -> ModuleLevel1.ModuleLevel1.Model.ModelB.Value",
            "ModuleLevel1.ModuleLevel1.Model.ModelB.Initialize -> ModuleLevel1.ModuleLevel1.Model.ModelC.RecursiveFuncOnModelC",
            "ModuleLevel1.ModuleLevel1.Model.ModelB.Initialize -> ModuleLevel1.ModuleLevel1.Model.StructA.Fill",
            "ModuleLevel1.ModuleLevel1.Model.ModelC.RecursiveFuncOnModelC -> ModuleLevel1.ModuleLevel1.Model.ModelC.RecursiveFuncOnModelC",
            "ModuleLevel1.ModuleLevel1.Model.StructA.Fill -> ModuleLevel1.ModuleLevel1.Model.ModelB.Value",
            "ModuleLevel1.ModuleLevel1.ServiceC.Do -> ModuleLevel1.ModuleLevel1.Model.ModelA.GetModelC",
            "ModuleLevel1.ModuleLevel1.Model.ModelB.Do -> ModuleLevel1.ModuleLevel1.Model.ModelC.MethodOnModelC",
            "ModuleLevel1.ModuleLevel1.Model.ModelB.Do -> ModuleLevel1.ModuleLevel1.Model.ModelC.MethodOnModelCCalledFromLambda",
            "ModuleLevel1.ModuleLevel1.ServiceC.Do -> ModuleLevel1.ModuleLevel1.Model.ModelA.GetModelD",
            "ModuleLevel1.ModuleLevel1.ServiceC.Do -> ModuleLevel1.ModuleLevel1.ServiceC.Execute",
            "ModuleLevel1.ModuleLevel1.ServiceC.Do -> ModuleLevel2.ModuleLevel2.Utility.UtilityMethod1",
            "ModuleLevel2.ModuleLevel2.SelfReferencingClass..ctor -> ModuleLevel2.ModuleLevel2.SelfReferencingClass.CommitHash",
            "ModuleLevel2.ModuleLevel2.Utility.UtilityMethod1 -> ModuleLevel2.ModuleLevel2.Utility.UtilityMethod2",
            "ModuleLevel2.ModuleLevel2.Utility.UtilityMethod2 -> ModuleLevel2.ModuleLevel2.Utility.UtilityMethod1",

            // Property calling a property
            "ModuleLevel1.ModuleLevel1.Model.ModelA.ModelCPropertyOfModelA -> ModuleLevel1.ModuleLevel1.Model.ModelC.IntPropertyOfModelC",

            // Property calling a function
            "ModuleLevel1.ModuleLevel1.Model.ModelC.IntPropertyOfModelC -> ModuleLevel1.ModuleLevel1.Model.ModelC.RecursiveFuncOnModelC",

            // Access to properties, setter and getter included.
            "ModuleLevel1.ModuleLevel1.Model.ModelA.AccessToPropertiesGetter -> ModuleLevel1.ModuleLevel1.Model.ModelA.ModelCPropertyOfModelA",
            "ModuleLevel1.ModuleLevel1.Model.ModelA.AccessToPropertiesSetter -> ModuleLevel1.ModuleLevel1.Model.ModelA.ModelCPropertyOfModelA",
            
            "CSharpLanguage.CSharpLanguage.EventInvocation.DoSomething -> CSharpLanguage.CSharpLanguage.EventInvocation.Raise1",
            "CSharpLanguage.CSharpLanguage.EventInvocation.DoSomething -> CSharpLanguage.CSharpLanguage.EventInvocation.Raise2",
            "CSharpLanguage.CSharpLanguage.EventInvocation.DoSomething -> CSharpLanguage.CSharpLanguage.EventInvocation.Raise3"
        };

        CollectionAssert.AreEquivalent(expected, actual);
    }

    [Test]
    public void FindsAllProperties()
    {
        var codeElements = _graph.Nodes.Values;

        var properties = codeElements
            .Where(n => n.ElementType == CodeElementType.Property)
            .Select(m => m.FullName);

        var expectedProperties = new HashSet<string>
        {
            "ModuleLevel1.ModuleLevel1.IServiceC.IfProperty",
            "ModuleLevel1.ModuleLevel1.Model.ModelA.ModelCPropertyOfModelA",
            "ModuleLevel1.ModuleLevel1.Model.ModelB.Value",
            "ModuleLevel1.ModuleLevel1.Model.ModelC.IntPropertyOfModelC",
            "ModuleLevel1.ModuleLevel1.Model.StructA.DependencyToConstant",
            "ModuleLevel1.ModuleLevel1.ServiceBase.IfProperty", "ModuleLevel1.ModuleLevel1.ServiceC.IfProperty",
            "ModuleLevel2.ModuleLevel2.SelfReferencingClass.Commit",
            "ModuleLevel2.ModuleLevel2.SelfReferencingClass.CommitHash",
            "ModuleLevel2.ModuleLevel2.SelfReferencingClass.Parents",
            "ModuleLevel2.ModuleLevel2.SelfReferencingClass.Children"
        };


        CollectionAssert.AreEquivalent(expectedProperties, properties);
    }


    [Test]
    public void FindsAllPropertyImplementations()
    {
        // Realize an interface
        var actual = _graph.Nodes.Values
            .SelectMany(n => n.Dependencies)
            .Where(d => d.Type == DependencyType.Implements)
            .Select(d => (_graph.Nodes[d.SourceId], _graph.Nodes[d.TargetId]))
            .Where(t => t.Item1.ElementType == CodeElementType.Property &&
                        t.Item2.ElementType == CodeElementType.Property)
            .Select(t => $"{t.Item1.FullName} -> {t.Item2.FullName}")
            .ToList();


        var expected = new HashSet<string>
        {
            "ModuleLevel1.ModuleLevel1.ServiceBase.IfProperty -> ModuleLevel1.ModuleLevel1.IServiceC.IfProperty"
        };


        CollectionAssert.AreEquivalent(expected, actual);
    }

    [Test]
    public void FindsAllPropertyOverrides()
    {
        var actual = _graph.Nodes.Values
            .SelectMany(n => n.Dependencies)
            .Where(d => d.Type == DependencyType.Overrides)
            .Select(d => (_graph.Nodes[d.SourceId], _graph.Nodes[d.TargetId]))
            .Where(t => t.Item1.ElementType == CodeElementType.Property &&
                        t.Item2.ElementType == CodeElementType.Property)
            .Select(t => $"{t.Item1.FullName} -> {t.Item2.FullName}")
            .ToList();


        var expected = new HashSet<string>
        {
            "ModuleLevel1.ModuleLevel1.ServiceC.IfProperty -> ModuleLevel1.ModuleLevel1.ServiceBase.IfProperty"
        };


        CollectionAssert.AreEquivalent(expected, actual);
    }

    [Test]
    public void FindsAllMethodImplementations()
    {
        var actual = _graph.Nodes.Values
            .SelectMany(n => n.Dependencies)
            .Where(d => d.Type == DependencyType.Implements)
            .Select(d => (_graph.Nodes[d.SourceId], _graph.Nodes[d.TargetId]))
            .Where(t => t.Item1.ElementType == CodeElementType.Method &&
                        t.Item2.ElementType == CodeElementType.Method)
            .Select(t => $"{t.Item1.FullName} -> {t.Item2.FullName}")
            .ToList();


        var expected = new HashSet<string>
        {
            "ModuleLevel1.ModuleLevel1.ServiceBase.Do -> ModuleLevel1.ModuleLevel1.IServiceC.Do",
            "CSharpLanguage.CSharpLanguage.MissingInterface.BaseStorage.Load -> CSharpLanguage.CSharpLanguage.MissingInterface.IStorage.Load",
            "CSharpLanguage.CSharpLanguage.StructWithInterface.Method -> CSharpLanguage.CSharpLanguage.IStructInterface.Method"
        };


        CollectionAssert.AreEquivalent(expected, actual);
    }


    [Test]
    public void FindsAllMethodOverrides()
    {
        var actual = _graph.Nodes.Values
            .SelectMany(n => n.Dependencies)
            .Where(d => d.Type == DependencyType.Overrides)
            .Select(d => (_graph.Nodes[d.SourceId], _graph.Nodes[d.TargetId]))
            .Where(t => t.Item1.ElementType == CodeElementType.Method &&
                        t.Item2.ElementType == CodeElementType.Method)
            .Select(t => $"{t.Item1.FullName} -> {t.Item2.FullName}")
            .ToList();


        var expected = new HashSet<string>
        {
            "ModuleLevel1.ModuleLevel1.ServiceC.Do -> ModuleLevel1.ModuleLevel1.ServiceBase.Do"
        };


        CollectionAssert.AreEquivalent(expected, actual);
    }


    [Test]
    public void FindsAllEventUsages()
    {
        // Registration and un-registration
        var actual = _graph.Nodes.Values
            .SelectMany(n => n.Dependencies)
            .Where(d => d.Type == DependencyType.Uses)
            .Select(d => (_graph.Nodes[d.SourceId], _graph.Nodes[d.TargetId]))
            .Where(t => t.Item2.ElementType == CodeElementType.Event)
            .Select(t => $"{t.Item1.FullName} -> {t.Item2.FullName}")
            .ToList();


        var expected = new HashSet<string>
        {
            "CSharpLanguage.CSharpLanguage.ClassUsingAnEvent.Init -> CSharpLanguage.CSharpLanguage.ClassOfferingAnEvent.MyEvent1",
            "CSharpLanguage.CSharpLanguage.ClassUsingAnEvent.Init -> CSharpLanguage.CSharpLanguage.ClassOfferingAnEvent.MyEvent2", 
            "CSharpLanguage.CSharpLanguage.EventSink..ctor -> CSharpLanguage.CSharpLanguage.IInterfaceWithEvent.MyEvent"
        };


        CollectionAssert.AreEquivalent(expected, actual);
    }


    [Test]
    public void FindsAllEventInvocation()
    {
        var actual = _graph.Nodes.Values
            .SelectMany(n => n.Dependencies)
            .Where(d => d.Type == DependencyType.Invokes)
            .Select(d => (_graph.Nodes[d.SourceId], _graph.Nodes[d.TargetId]))
            .Select(t => $"{t.Item1.FullName} -> {t.Item2.FullName}")
            .ToList();


        var expected = new HashSet<string>
        {
            "CSharpLanguage.CSharpLanguage.ClassOfferingAnEvent.OnEvent -> CSharpLanguage.CSharpLanguage.ClassOfferingAnEvent.MyEvent2",
            "CSharpLanguage.CSharpLanguage.EventInvocation.Raise1 -> CSharpLanguage.CSharpLanguage.EventInvocation.MyEvent",
            "CSharpLanguage.CSharpLanguage.EventInvocation.Raise2 -> CSharpLanguage.CSharpLanguage.EventInvocation.MyEvent",
            "CSharpLanguage.CSharpLanguage.EventInvocation.Raise3 -> CSharpLanguage.CSharpLanguage.EventInvocation.MyEvent"
        };


        CollectionAssert.AreEquivalent(expected, actual);
    }


    [Test]
    public void FindsAllEventImplementations()
    {
        // Registration and unregistration
        var actual = _graph.Nodes.Values
            .SelectMany(n => n.Dependencies)
            .Where(d => d.Type == DependencyType.Implements)
            .Select(d => (_graph.Nodes[d.SourceId], _graph.Nodes[d.TargetId]))
            .Where(t => t.Item1.ElementType == CodeElementType.Event &&
                        t.Item2.ElementType == CodeElementType.Event)
            .Select(t => $"{t.Item1.FullName} -> {t.Item2.FullName}")
            .ToList();


        var expected = new HashSet<string>
        {
            "CSharpLanguage.CSharpLanguage.EventInvocation.MyEvent -> CSharpLanguage.CSharpLanguage.IInterfaceWithEvent.MyEvent"
        };


        CollectionAssert.AreEquivalent(expected, actual);
    }


    [Test]
    public void FindsAllEventHandlers()
    {
        var actual = _graph.Nodes.Values
            .SelectMany(n => n.Dependencies)
            .Where(d => d.Type == DependencyType.Handles)
            .Select(d => (_graph.Nodes[d.SourceId], _graph.Nodes[d.TargetId]))      
            .Select(t => $"{t.Item1.FullName} -> {t.Item2.FullName}")
            .ToList();


        var expected = new HashSet<string>
        {
            "CSharpLanguage.CSharpLanguage.ClassUsingAnEvent.MyEventHandler -> CSharpLanguage.CSharpLanguage.ClassOfferingAnEvent.MyEvent1", 
            "CSharpLanguage.CSharpLanguage.ClassUsingAnEvent.MyEventHandler2 -> CSharpLanguage.CSharpLanguage.ClassOfferingAnEvent.MyEvent2", 
            "CSharpLanguage.CSharpLanguage.EventSink.Handler -> CSharpLanguage.CSharpLanguage.IInterfaceWithEvent.MyEvent"
        };


        CollectionAssert.AreEquivalent(expected, actual);
    }


    [Test]
    public void FindsAllInterfaceImplementations()
    {
        var actual = _graph.Nodes.Values
            .SelectMany(n => n.Dependencies)
            .Where(d => d.Type == DependencyType.Implements)
            .Select(d => (_graph.Nodes[d.SourceId], _graph.Nodes[d.TargetId]))
            .Where(t => t.Item1.ElementType == CodeElementType.Class &&
                        t.Item2.ElementType == CodeElementType.Interface)
            .Select(t => $"{t.Item1.FullName} -> {t.Item2.FullName}")
            .ToList();


        var expected = new HashSet<string>
        {
            "ModuleLevel1.ModuleLevel1.ServiceBase -> ModuleLevel1.ModuleLevel1.IServiceC",
            "CSharpLanguage.CSharpLanguage.MissingInterface.Storage -> CSharpLanguage.CSharpLanguage.MissingInterface.IStorage",
            "CSharpLanguage.CSharpLanguage.EventInvocation -> CSharpLanguage.CSharpLanguage.IInterfaceWithEvent"
        };


        CollectionAssert.AreEquivalent(expected, actual);
    }

    [Test]
    public void FindsAllInheritance()
    {
        var actual = _graph.Nodes.Values
            .SelectMany(n => n.Dependencies)
            .Where(d => d.Type == DependencyType.Inherits)
            .Select(d => (_graph.Nodes[d.SourceId], _graph.Nodes[d.TargetId]))
            .Where(t => t.Item1.ElementType == CodeElementType.Class &&
                        t.Item2.ElementType == CodeElementType.Class)
            .Select(t => $"{t.Item1.FullName} -> {t.Item2.FullName}")
            .ToList();


        var expected = new HashSet<string>
        {
            "ModuleLevel1.ModuleLevel1.ServiceC -> ModuleLevel1.ModuleLevel1.ServiceBase",
            "CSharpLanguage.CSharpLanguage.ProjectFile -> CSharpLanguage.CSharpLanguage.XmlFile",
            "CSharpLanguage.CSharpLanguage.MissingInterface.Storage -> CSharpLanguage.CSharpLanguage.MissingInterface.BaseStorage"
        };


        CollectionAssert.AreEquivalent(expected, actual);
    }


    [Test]
    public void FindsAllUsingBetweenClasses()
    {
        var actual = _graph.Nodes.Values
            .SelectMany(n => n.Dependencies)
            .Where(d => d.Type == DependencyType.Uses)
            .Select(d => (_graph.Nodes[d.SourceId], _graph.Nodes[d.TargetId]))
            .Where(t => t.Item1.ElementType == CodeElementType.Class &&
                        t.Item2.ElementType == CodeElementType.Class)
            .Select(t => $"{t.Item1.FullName} -> {t.Item2.FullName}")
            .ToList();


        var expected = new HashSet<string>
        {
            "ModuleLevel2.ModuleLevel2.DerivedFromGenericSystemClass -> ModuleLevel2.ModuleLevel2.SelfReferencingClass",
            "CSharpLanguage.CSharpLanguage.ProjectFile -> CSharpLanguage.CSharpLanguage.Project"
        };


        CollectionAssert.AreEquivalent(expected, actual);
    }

    [Test]
    public void Find_Interface_Implementation_In_BaseClass()
    {
        // This case is not covered yet
        var iStorage = _graph.Nodes.Values.Single(n => n.Name == "IStorage");
        var baseStorage = _graph.Nodes.Values.Single(n => n.Name == "BaseStorage");
        var storage = _graph.Nodes.Values.Single(n => n.Name == "Storage");
        var iLoad = iStorage.Children.Single();
        var load = baseStorage.Children.Single();

        Assert.IsTrue(storage.Dependencies.Any(d => d.TargetId == iStorage.Id && d.Type == DependencyType.Implements));
        Assert.IsTrue(storage.Dependencies.Any(d => d.TargetId == baseStorage.Id && d.Type == DependencyType.Inherits));

        // Not detected!
        Assert.IsTrue(load.Dependencies.Any(d => d.TargetId == iLoad.Id && d.Type == DependencyType.Implements));
    }

    [Test]
    public void Find_all_cycles()
    {
        var result = CycleFinder.FindCycleGroups(_graph);
        Assert.AreEqual(8, result.Count);
    }
}