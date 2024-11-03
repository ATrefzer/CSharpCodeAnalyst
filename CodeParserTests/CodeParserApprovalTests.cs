using CodeParser.Analysis.Cycles;
using CodeParser.Parser;
using CodeParser.Parser.Config;
using Contracts.Graph;
using CSharpCodeAnalyst.Exploration;

namespace CodeParserTests;

public class CodeParserApprovalTests
{
    private CodeGraph _graph;

    [OneTimeSetUp]
    public async Task FixtureSetup()
    {
        Initializer.InitializeMsBuildLocator();
        var parser = new Parser(new ParserConfig(new ProjectExclusionRegExCollection(), 1));
        _graph = await parser.ParseSolution(@"..\..\..\..\SampleProject\SampleProject.sln");
    }

    /// <summary>
    ///     This test is actually wrong(!) If we could recognize that AddSlave is a call to another object we could be more
    ///     precise. In this case we would add the base call from the second instance.
    /// </summary>
    [Test]
    public void CodeExplorer_FollowIncomingCalls_1()
    {
        // Scenario where base class calls base method of another instance.
        var codeElements = _graph.Nodes.Values;

        var explorer = new CodeGraphExplorer();
        explorer.LoadCodeGraph(_graph);

        var origin = codeElements.First(e =>
            e.FullName.Contains("Regression_FollowIncomingCalls1.ViewModelAdapter1.AddToSlave"));
        var result = explorer.FollowIncomingCallsRecursive(origin.Id);

        var actualRelationships = result.Relationships.Select(d =>
                $"{_graph.Nodes[d.SourceId].FullName} -({d.Type})-> {_graph.Nodes[d.TargetId].FullName}")
            .OrderBy(x => x);

        var expectedRelationships = new List<string>
        {
            // Self call but not recognized that this is on another object
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave -(Calls)-> CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.Build -(Calls)-> CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Driver..ctor -(Calls)-> CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.Build",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter1.AddToSlave -(Overrides)-> CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave"
        };
        CollectionAssert.AreEquivalent(expectedRelationships, actualRelationships);


        var actualElements = result.Elements.Select(m => m.FullName).ToList();

        var expectedElements = new List<string>
        {
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter1.AddToSlave",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.Build",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Driver..ctor"
        };
        CollectionAssert.AreEquivalent(expectedElements, actualElements);
    }

    [Test]
    public void CodeExplorer_FollowIncomingCalls_2()
    {
        var codeElements = _graph.Nodes.Values;

        var explorer = new CodeGraphExplorer();
        explorer.LoadCodeGraph(_graph);

        var origin = codeElements.First(e =>
            e.FullName.Contains("Regression_FollowIncomingCalls2.ViewModelAdapter1.AddToSlave"));
        var result = explorer.FollowIncomingCallsRecursive(origin.Id);

        var actualRelationships = result.Relationships.Select(d =>
            $"{_graph.Nodes[d.SourceId].FullName} -({d.Type})-> {_graph.Nodes[d.TargetId].FullName}");
        var expectedRelationships = new List<string>
        {
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base.Build -(Calls)-> CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base.AddToSlave",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Driver..ctor -(Calls)-> CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base.Build",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter1.AddToSlave -(Overrides)-> CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base.AddToSlave"
        };
        CollectionAssert.AreEquivalent(expectedRelationships, actualRelationships);


        var actualElements = result.Elements.Select(m => m.FullName).ToList();

        var expectedElements = new List<string>
        {
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base.AddToSlave",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter1.AddToSlave",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base.Build",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Driver..ctor"
        };
        CollectionAssert.AreEquivalent(expectedElements, actualElements);
    }

    [Test]
    public void FindsAllAssemblies()
    {
        var codeElements = _graph.Nodes.Values;

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
            "CSharpLanguage.CSharpLanguage.EventSink.Handler",

            "CSharpLanguage.CSharpLanguage.Partial.Client.CreateInstance",
            "CSharpLanguage.CSharpLanguage.Partial.PartialClass.MethodInPartialClassPart1",
            "CSharpLanguage.CSharpLanguage.Partial.PartialClass.MethodInPartialClassPart2",

            // Interface implementation from different assembly
            "ModuleLevel0.ModuleLevel2.InterfaceImplementerInDifferentCompilation.Method",
            "ModuleLevel2.ModuleLevel0.InterfaceInDifferentCompilation.Method",

            // Regression Hierarchies
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.InterfaceBase.MethodFromInterfaceBase",
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.InterfaceA.MethodA",
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.InterfaceB.MethodB",
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.InterfaceC.MethodC",
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassBase.MethodA",
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived1.MethodB",
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived1.MethodC",
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived1.MethodFromInterfaceBase",
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived2.MethodA",
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived2.MethodC",
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived3.MethodB",
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived4.MethodA",

            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.Build",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Driver..ctor",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter1.AddToSlave",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter2.AddToSlave",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base.AddToSlave",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base.Build",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Driver..ctor",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter1.AddToSlave",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter2.AddToSlave"
        };


        CollectionAssert.AreEquivalent(expectedMethods, methods);
    }

    [Test]
    public void FindsAllClasses()
    {
        var codeElements = _graph.Nodes.Values;

        var actual = codeElements
            .Where(n => n.ElementType == CodeElementType.Class)
            .Select(m => m.FullName).ToList();

        var expected = new List<string>
        {
            // Twice this is a generic class
            "CSharpLanguage.CSharpLanguage.MoreGenerics.Foo",
            "CSharpLanguage.CSharpLanguage.MoreGenerics.Foo",

            "ModuleLevel2.ModuleLevel2.DerivedFromGenericSystemClass",
            "ModuleLevel2.ModuleLevel2.N1.ClassInNs1", "ModuleLevel2.ModuleLevel2.N1.N2.N3.ClassInNs2",
            "ModuleLevel2.ModuleLevel2.SelfReferencingClass", "ModuleLevel2.ModuleLevel2.Utility",
            "ModuleLevel1.ModuleLevel1.Model.ModelC",
            "ModuleLevel1.ModuleLevel1.Model.ModelD", "ModuleLevel1.ModuleLevel1.ServiceA",
            "ModuleLevel1.ModuleLevel1.ServiceBase", "ModuleLevel1.ModuleLevel1.ServiceC",
            "ModuleLevel2.ClassInGlobalNs", "ModuleLevel2.Insight.Analyzers",
            "ModuleLevel2.Insight.Dialogs.TrendViewModel", "ModuleLevel2.ModuleLevel2.Constants",
            "Cycles.Cycles.OuterClass.MiddleClass.NestedInnerClass",
            "ModuleLevel0.ModuleLevel0.Bootstrapper", "ModuleLevel0.ModuleLevel0.Ns1.ClassL",
            "ModuleLevel0.ModuleLevel0.Ns1.ClassL.InnerClassL", "ModuleLevel0.ModuleLevel0.Ns1.Ns2.ClassY",
            "ModuleLevel0.ModuleLevel2.InterfaceImplementerInDifferentCompilation",
            "ModuleLevel1.ModuleLevel1.FactoryC", "ModuleLevel1.ModuleLevel1.Model.ModelA",
            "ModuleLevel1.ModuleLevel1.Model.ModelB",

            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived3",
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived4",
            "CSharpLanguage.CSharpLanguage.TheExtendedType", "CSharpLanguage.CSharpLanguage.XmlFile",
            "Cycles.Cycles.ClassLevel_Fields.Class1", "Cycles.Cycles.ClassLevel_Fields.Class2",
            "Cycles.Cycles.OuterClass", "Cycles.Cycles.OuterClass.DirectChildClass",
            "Cycles.Cycles.OuterClass.MiddleClass",
            "CSharpLanguage.CSharpLanguage.Partial.Client",
            "CSharpLanguage.CSharpLanguage.Partial.PartialClass", "CSharpLanguage.CSharpLanguage.PinSignalView",
            "CSharpLanguage.CSharpLanguage.PinSignalView.PinSignalViewAutomationPeer",
            "CSharpLanguage.CSharpLanguage.Project", "CSharpLanguage.CSharpLanguage.ProjectFile",
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassBase",
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived1",
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived2",

            "CSharpLanguage.CSharpLanguage.My2Attribute", "CSharpLanguage.CSharpLanguage.MyAttribute",
            "CSharpLanguage.CSharpLanguage.MyEventArgs", "CSharpLanguage.CSharpLanguage.NS_Parent.ClassINparent",
            "CSharpLanguage.CSharpLanguage.NS_Parent.NS_Child.ClassNsChild",
            "CSharpLanguage.CSharpLanguage.NS_Parent.NS_Irrelevant.ClassNsIrrelevant",
            "CSharpLanguage.CSharpLanguage.Ns1.ClassM", "CSharpLanguage.CSharpLanguage.Ns1.Ns2.ClassX",
            "CSharpLanguage.CSharpLanguage.ClassOfferingAnEvent", "CSharpLanguage.CSharpLanguage.ClassUsingAnEvent",
            "CSharpLanguage.CSharpLanguage.CreatorOfGenericTypes", "CSharpLanguage.CSharpLanguage.CustomException",
            "CSharpLanguage.CSharpLanguage.EventInvocation", "CSharpLanguage.CSharpLanguage.EventSink",
            "CSharpLanguage.CSharpLanguage.Extensions", "CSharpLanguage.CSharpLanguage.MissingInterface.BaseStorage",
            "CSharpLanguage.CSharpLanguage.MissingInterface.Storage", "CSharpLanguage.CSharpLanguage.MoreGenerics",

            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Driver",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter1",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter2",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Driver",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter1",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter2"
        };

        CollectionAssert.AreEquivalent(expected, actual);
    }

    [Test]
    public void FindsAllCalls()
    {
        var calls = _graph.GetAllRelationships().Where(d => d.Type == RelationshipType.Calls);

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
            "CSharpLanguage.CSharpLanguage.EventInvocation.DoSomething -> CSharpLanguage.CSharpLanguage.EventInvocation.Raise3",

            "CSharpLanguage.CSharpLanguage.Partial.Client.CreateInstance -> CSharpLanguage.CSharpLanguage.Partial.PartialClass.MethodInPartialClassPart1",
            "CSharpLanguage.CSharpLanguage.Partial.Client.CreateInstance -> CSharpLanguage.CSharpLanguage.Partial.PartialClass.MethodInPartialClassPart2",


            // Hierarchies. Calling the base classes.
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived2.MethodA -> CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassBase.MethodA",
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived4.MethodA -> CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived2.MethodA",


            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave -> CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.Build -> CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Driver..ctor -> CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.Build",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter1.AddToSlave -> CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter2.AddToSlave -> CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base.Build -> CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base.AddToSlave",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Driver..ctor -> CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base.Build",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter1.AddToSlave -> CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base.AddToSlave",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter2.AddToSlave -> CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base.AddToSlave"
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
            .SelectMany(n => n.Relationships)
            .Where(d => d.Type == RelationshipType.Implements)
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
            .SelectMany(n => n.Relationships)
            .Where(d => d.Type == RelationshipType.Overrides)
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
            .SelectMany(n => n.Relationships)
            .Where(d => d.Type == RelationshipType.Implements)
            .Select(d => (_graph.Nodes[d.SourceId], _graph.Nodes[d.TargetId]))
            .Where(t => t.Item1.ElementType == CodeElementType.Method &&
                        t.Item2.ElementType == CodeElementType.Method)
            .Select(t => $"{t.Item1.FullName} -> {t.Item2.FullName}")
            .ToList();


        var expected = new HashSet<string>
        {
            "ModuleLevel1.ModuleLevel1.ServiceBase.Do -> ModuleLevel1.ModuleLevel1.IServiceC.Do",
            "CSharpLanguage.CSharpLanguage.MissingInterface.BaseStorage.Load -> CSharpLanguage.CSharpLanguage.MissingInterface.IStorage.Load",
            "CSharpLanguage.CSharpLanguage.StructWithInterface.Method -> CSharpLanguage.CSharpLanguage.IStructInterface.Method",

            // Regression Hierarchies
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassBase.MethodA -> CSharpLanguage.CSharpLanguage.Regression_Hierarchies.InterfaceA.MethodA",
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived1.MethodB -> CSharpLanguage.CSharpLanguage.Regression_Hierarchies.InterfaceB.MethodB",
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived1.MethodFromInterfaceBase -> CSharpLanguage.CSharpLanguage.Regression_Hierarchies.InterfaceBase.MethodFromInterfaceBase",
            // Yes, even if it is abstract. It is still an implementation.
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived1.MethodC -> CSharpLanguage.CSharpLanguage.Regression_Hierarchies.InterfaceC.MethodC",


            "ModuleLevel0.ModuleLevel2.InterfaceImplementerInDifferentCompilation.Method -> ModuleLevel2.ModuleLevel0.InterfaceInDifferentCompilation.Method"
        };


        CollectionAssert.AreEquivalent(expected, actual);
    }


    [Test]
    public void FindsAllMethodOverrides()
    {
        var actual = _graph.Nodes.Values
            .SelectMany(n => n.Relationships)
            .Where(d => d.Type == RelationshipType.Overrides)
            .Select(d => (_graph.Nodes[d.SourceId], _graph.Nodes[d.TargetId]))
            .Where(t => t.Item1.ElementType == CodeElementType.Method &&
                        t.Item2.ElementType == CodeElementType.Method)
            .Select(t => $"{t.Item1.FullName} -> {t.Item2.FullName}")
            .ToList();


        var expected = new HashSet<string>
        {
            "ModuleLevel1.ModuleLevel1.ServiceC.Do -> ModuleLevel1.ModuleLevel1.ServiceBase.Do",

            // Regression Hierarchies
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived2.MethodA -> CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassBase.MethodA",
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived2.MethodC -> CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived1.MethodC",
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived3.MethodB -> CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived1.MethodB",
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived4.MethodA -> CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived2.MethodA",

            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter1.AddToSlave -> CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter2.AddToSlave -> CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter1.AddToSlave -> CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base.AddToSlave",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter2.AddToSlave -> CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base.AddToSlave"
        };


        CollectionAssert.AreEquivalent(expected, actual);
    }


    [Test]
    public void FindsAllEventUsages()
    {
        // Registration and un-registration
        var actual = _graph.Nodes.Values
            .SelectMany(n => n.Relationships)
            .Where(d => d.Type == RelationshipType.Uses)
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
            .SelectMany(n => n.Relationships)
            .Where(d => d.Type == RelationshipType.Invokes)
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
        var actual = _graph.Nodes.Values
            .SelectMany(n => n.Relationships)
            .Where(d => d.Type == RelationshipType.Implements)
            .Select(d => (_graph.Nodes[d.SourceId], _graph.Nodes[d.TargetId]))
            .Where(t => t.Item1.ElementType == CodeElementType.Event &&
                        t.Item2.ElementType == CodeElementType.Event)
            .Select(t => $"{t.Item1.FullName} -> {t.Item2.FullName}")
            .ToList();


        var expected = new HashSet<string>
        {
            "CSharpLanguage.CSharpLanguage.EventInvocation.MyEvent -> CSharpLanguage.CSharpLanguage.IInterfaceWithEvent.MyEvent",

            "ModuleLevel0.ModuleLevel2.InterfaceImplementerInDifferentCompilation.AEvent -> ModuleLevel2.ModuleLevel0.InterfaceInDifferentCompilation.AEvent"
        };


        CollectionAssert.AreEquivalent(expected, actual);
    }


    [Test]
    public void FindsAllEventHandlers()
    {
        var actual = _graph.Nodes.Values
            .SelectMany(n => n.Relationships)
            .Where(d => d.Type == RelationshipType.Handles)
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
            .SelectMany(n => n.Relationships)
            .Where(d => d.Type == RelationshipType.Implements)
            .Select(d => (_graph.Nodes[d.SourceId], _graph.Nodes[d.TargetId]))
            .Where(t => (t.Item1.ElementType == CodeElementType.Class ||
                         t.Item1.ElementType == CodeElementType.Interface) &&
                        t.Item2.ElementType == CodeElementType.Interface)
            .Select(t => $"{t.Item1.FullName} -> {t.Item2.FullName}")
            .ToList();


        var expected = new HashSet<string>
        {
            "ModuleLevel1.ModuleLevel1.ServiceBase -> ModuleLevel1.ModuleLevel1.IServiceC",
            "CSharpLanguage.CSharpLanguage.MissingInterface.Storage -> CSharpLanguage.CSharpLanguage.MissingInterface.IStorage",
            "CSharpLanguage.CSharpLanguage.EventInvocation -> CSharpLanguage.CSharpLanguage.IInterfaceWithEvent",


            // Regression Hierarchies
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.InterfaceC -> CSharpLanguage.CSharpLanguage.Regression_Hierarchies.InterfaceBase",
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassBase -> CSharpLanguage.CSharpLanguage.Regression_Hierarchies.InterfaceA",
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived1 -> CSharpLanguage.CSharpLanguage.Regression_Hierarchies.InterfaceB",
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived1 -> CSharpLanguage.CSharpLanguage.Regression_Hierarchies.InterfaceC",


            "ModuleLevel0.ModuleLevel2.InterfaceImplementerInDifferentCompilation -> ModuleLevel2.ModuleLevel0.InterfaceInDifferentCompilation"
        };


        CollectionAssert.AreEquivalent(expected, actual);
    }

    [Test]
    public void FindsAllInheritance()
    {
        var actual = _graph.Nodes.Values
            .SelectMany(n => n.Relationships)
            .Where(d => d.Type == RelationshipType.Inherits)
            .Select(d => (_graph.Nodes[d.SourceId], _graph.Nodes[d.TargetId]))
            .Where(t => t.Item1.ElementType == CodeElementType.Class &&
                        t.Item2.ElementType == CodeElementType.Class)
            .Select(t => $"{t.Item1.FullName} -> {t.Item2.FullName}")
            .ToList();


        var expected = new HashSet<string>
        {
            "ModuleLevel1.ModuleLevel1.ServiceC -> ModuleLevel1.ModuleLevel1.ServiceBase",
            "CSharpLanguage.CSharpLanguage.ProjectFile -> CSharpLanguage.CSharpLanguage.XmlFile",
            "CSharpLanguage.CSharpLanguage.MissingInterface.Storage -> CSharpLanguage.CSharpLanguage.MissingInterface.BaseStorage",

            // Regression Hierarchies
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived1 -> CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassBase",
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived2 -> CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived1",
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived3 -> CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived2",
            "CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived4 -> CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived3",


            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter1 -> CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter2 -> CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter1 -> CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base",
            "CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter2 -> CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base"
        };


        CollectionAssert.AreEquivalent(expected, actual);
    }


    [Test]
    public void FindsAllUsingBetweenClasses()
    {
        var actual = _graph.Nodes.Values
            .SelectMany(n => n.Relationships)
            .Where(d => d.Type == RelationshipType.Uses)
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

        Assert.IsTrue(
            storage.Relationships.Any(d => d.TargetId == iStorage.Id && d.Type == RelationshipType.Implements));
        Assert.IsTrue(
            storage.Relationships.Any(d => d.TargetId == baseStorage.Id && d.Type == RelationshipType.Inherits));

        // Not detected!
        Assert.IsTrue(load.Relationships.Any(d => d.TargetId == iLoad.Id && d.Type == RelationshipType.Implements));
    }

    [Test]
    public void Find_all_cycles()
    {
        var result = CycleFinder.FindCycleGroups(_graph);
        Assert.AreEqual(8, result.Count);
    }
}