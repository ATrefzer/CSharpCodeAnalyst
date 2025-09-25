using Contracts.Graph;

// ReSharper disable StringLiteralTypo

namespace CodeParserTests.ApprovalTests;

/// <summary>
///     Old legacy tests
/// </summary>
public class OldCSharpLanguageApprovalTests : ProjectTestBase
{
    private CodeGraph GetTestAssemblyGraph()
    {
        return GetAssemblyGraph("Old.CSharpLanguage");
    }

    [Test]
    public void FindsAllFunctions()
    {
        var codeElements = GetTestAssemblyGraph().Nodes.Values;

        var methods = codeElements
            .Where(n => n.ElementType == CodeElementType.Method)
            .Select(m => m.FullName).ToList();


        // No HashSet because some methods are overloaded
        var expectedMethods = new List<string>
        {
            "Old.CSharpLanguage.CSharpLanguage.ClassOfferingAnEvent.OnEvent",
            "Old.CSharpLanguage.CSharpLanguage.ClassUsingAnEvent.Init",
            "Old.CSharpLanguage.CSharpLanguage.ClassUsingAnEvent.MyEventHandler2",
            "Old.CSharpLanguage.CSharpLanguage.ClassUsingAnEvent.MyEventHandler",
            "Old.CSharpLanguage.CSharpLanguage.CreatorOfGenericTypes.Create",
            "Old.CSharpLanguage.CSharpLanguage.PinSignalView.OnCreteAutomationPeer",
            "Old.CSharpLanguage.CSharpLanguage.PinSignalView.PinSignalViewAutomationPeer..ctor",
            "Old.CSharpLanguage.CSharpLanguage.MissingInterface.BaseStorage.Load",
            "Old.CSharpLanguage.CSharpLanguage.MissingInterface.IStorage.Load",
            "Old.CSharpLanguage.CSharpLanguage.NS_Parent.NS_Child.ClassNsChild.Method",

            "Old.CSharpLanguage.CSharpLanguage.IStructInterface.Method",
            "Old.CSharpLanguage.CSharpLanguage.StructWithInterface.Method",

            // Extension method
            "Old.CSharpLanguage.CSharpLanguage.Extensions.Slice",

            // Called by extension method
            "Old.CSharpLanguage.CSharpLanguage.TheExtendedType.Do",

            // Generic method calls
            "Old.CSharpLanguage.CSharpLanguage.MoreGenerics.M1",
            "Old.CSharpLanguage.CSharpLanguage.MoreGenerics.M1",
            "Old.CSharpLanguage.CSharpLanguage.MoreGenerics.M2",
            "Old.CSharpLanguage.CSharpLanguage.MoreGenerics.M2",
            "Old.CSharpLanguage.CSharpLanguage.MoreGenerics.Run",


            // Show event registration
            "Old.CSharpLanguage.CSharpLanguage.EventInvocation.DoSomething",
            "Old.CSharpLanguage.CSharpLanguage.EventInvocation.Raise1",
            "Old.CSharpLanguage.CSharpLanguage.EventInvocation.Raise2",
            "Old.CSharpLanguage.CSharpLanguage.EventInvocation.Raise3",
            "Old.CSharpLanguage.CSharpLanguage.EventSink..ctor",
            "Old.CSharpLanguage.CSharpLanguage.EventSink.Handler",

            "Old.CSharpLanguage.CSharpLanguage.Partial.Client.CreateInstance",
            "Old.CSharpLanguage.CSharpLanguage.Partial.PartialClass.MethodInPartialClassPart1",
            "Old.CSharpLanguage.CSharpLanguage.Partial.PartialClass.MethodInPartialClassPart2",

            // Regression Hierarchies
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.InterfaceBase.MethodFromInterfaceBase",
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.InterfaceA.MethodA",
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.InterfaceB.MethodB",
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.InterfaceC.MethodC",
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassBase.MethodA",
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived1.MethodB",
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived1.MethodC",
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived1.MethodFromInterfaceBase",
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived2.MethodA",
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived2.MethodC",
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived3.MethodB",
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived4.MethodA",

            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.Build",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Driver..ctor",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter1.AddToSlave",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter2.AddToSlave",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base.AddToSlave",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base.Build",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Driver..ctor",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter1.AddToSlave",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter2.AddToSlave"
        };


        CollectionAssert.AreEquivalent(expectedMethods, methods);
    }

    [Test]
    public void FindsAllClasses()
    {
        var codeElements = GetTestAssemblyGraph().Nodes.Values;

        var actual = codeElements
            .Where(n => n.ElementType == CodeElementType.Class)
            .Select(m => m.FullName).ToList();

        var expected = new List<string>
        {
            // Twice this is a generic class
            "Old.CSharpLanguage.CSharpLanguage.MoreGenerics.Foo",
            "Old.CSharpLanguage.CSharpLanguage.MoreGenerics.Foo",


            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived3",
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived4",
            "Old.CSharpLanguage.CSharpLanguage.TheExtendedType",
            "Old.CSharpLanguage.CSharpLanguage.XmlFile",

            "Old.CSharpLanguage.CSharpLanguage.Partial.Client",
            "Old.CSharpLanguage.CSharpLanguage.Partial.PartialClass",
            "Old.CSharpLanguage.CSharpLanguage.PinSignalView",
            "Old.CSharpLanguage.CSharpLanguage.PinSignalView.PinSignalViewAutomationPeer",
            "Old.CSharpLanguage.CSharpLanguage.Project",
            "Old.CSharpLanguage.CSharpLanguage.ProjectFile",
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassBase",
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived1",
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived2",

            "Old.CSharpLanguage.CSharpLanguage.My2Attribute",
            "Old.CSharpLanguage.CSharpLanguage.MyAttribute",
            "Old.CSharpLanguage.CSharpLanguage.MyEventArgs",
            "Old.CSharpLanguage.CSharpLanguage.NS_Parent.ClassINparent",
            "Old.CSharpLanguage.CSharpLanguage.NS_Parent.NS_Child.ClassNsChild",
            "Old.CSharpLanguage.CSharpLanguage.NS_Parent.NS_Irrelevant.ClassNsIrrelevant",
            "Old.CSharpLanguage.CSharpLanguage.Ns1.ClassM",
            "Old.CSharpLanguage.CSharpLanguage.Ns1.Ns2.ClassX",
            "Old.CSharpLanguage.CSharpLanguage.ClassOfferingAnEvent",
            "Old.CSharpLanguage.CSharpLanguage.ClassUsingAnEvent",
            "Old.CSharpLanguage.CSharpLanguage.CreatorOfGenericTypes",
            "Old.CSharpLanguage.CSharpLanguage.CustomException",
            "Old.CSharpLanguage.CSharpLanguage.EventInvocation",
            "Old.CSharpLanguage.CSharpLanguage.EventSink",
            "Old.CSharpLanguage.CSharpLanguage.Extensions",
            "Old.CSharpLanguage.CSharpLanguage.MissingInterface.BaseStorage",
            "Old.CSharpLanguage.CSharpLanguage.MissingInterface.Storage",
            "Old.CSharpLanguage.CSharpLanguage.MoreGenerics",

            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Driver",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter1",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter2",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Driver",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter1",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter2"
        };

        CollectionAssert.AreEquivalent(expected, actual);
    }

    [Test]
    public void FindsAllCalls()
    {
        var actual = GetRelationshipsOfType(GetTestAssemblyGraph(), RelationshipType.Calls);

        var expected = new HashSet<string>
        {
            "Old.CSharpLanguage.CSharpLanguage.ClassUsingAnEvent.Init -> Old.CSharpLanguage.CSharpLanguage.Extensions.Slice",
            "Old.CSharpLanguage.CSharpLanguage.Extensions.Slice -> Old.CSharpLanguage.CSharpLanguage.TheExtendedType.Do",
            "Old.CSharpLanguage.CSharpLanguage.MoreGenerics.Run -> Old.CSharpLanguage.CSharpLanguage.MoreGenerics.M1",
            "Old.CSharpLanguage.CSharpLanguage.MoreGenerics.Run -> Old.CSharpLanguage.CSharpLanguage.MoreGenerics.M2",

            "Old.CSharpLanguage.CSharpLanguage.EventInvocation.DoSomething -> Old.CSharpLanguage.CSharpLanguage.EventInvocation.Raise1",
            "Old.CSharpLanguage.CSharpLanguage.EventInvocation.DoSomething -> Old.CSharpLanguage.CSharpLanguage.EventInvocation.Raise2",
            "Old.CSharpLanguage.CSharpLanguage.EventInvocation.DoSomething -> Old.CSharpLanguage.CSharpLanguage.EventInvocation.Raise3",

            "Old.CSharpLanguage.CSharpLanguage.Partial.Client.CreateInstance -> Old.CSharpLanguage.CSharpLanguage.Partial.PartialClass.MethodInPartialClassPart1",
            "Old.CSharpLanguage.CSharpLanguage.Partial.Client.CreateInstance -> Old.CSharpLanguage.CSharpLanguage.Partial.PartialClass.MethodInPartialClassPart2",


            // Hierarchies. Calling the base classes.
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived2.MethodA -> Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassBase.MethodA",
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived4.MethodA -> Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived2.MethodA",


            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave -> Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.Build -> Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Driver..ctor -> Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.Build",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter1.AddToSlave -> Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter2.AddToSlave -> Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base.Build -> Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base.AddToSlave",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Driver..ctor -> Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base.Build",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter1.AddToSlave -> Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base.AddToSlave",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter2.AddToSlave -> Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base.AddToSlave"
        };

        CollectionAssert.AreEquivalent(expected, actual);
    }

    [Test]
    public void FindsAllUses()
    {
        var actual = GetRelationshipsOfType(GetTestAssemblyGraph(), RelationshipType.Uses);
        var expected = new HashSet<string>
        {
            "Old.CSharpLanguage.CSharpLanguage.MyDelegate -> Old.CSharpLanguage.CSharpLanguage.MyEventArgs",
            "Old.CSharpLanguage.CSharpLanguage.ClassOfferingAnEvent.MyEvent1 -> Old.CSharpLanguage.CSharpLanguage.MyDelegate",
            "Old.CSharpLanguage.CSharpLanguage.ClassUsingAnEvent.Init -> Old.CSharpLanguage.CSharpLanguage.ClassOfferingAnEvent.MyEvent1",
            "Old.CSharpLanguage.CSharpLanguage.ClassUsingAnEvent.Init -> Old.CSharpLanguage.CSharpLanguage.ClassOfferingAnEvent.MyEvent2",
            "Old.CSharpLanguage.CSharpLanguage.ClassUsingAnEvent.MyEventHandler -> Old.CSharpLanguage.CSharpLanguage.MyEventArgs",
            "Old.CSharpLanguage.CSharpLanguage.ProjectFile -> Old.CSharpLanguage.CSharpLanguage.Project",
            "Old.CSharpLanguage.CSharpLanguage.CreatorOfGenericTypes._file -> Old.CSharpLanguage.CSharpLanguage.ProjectFile",
            "Old.CSharpLanguage.CSharpLanguage.CreatorOfGenericTypes.Create -> Old.CSharpLanguage.CSharpLanguage.Project",
            "Old.CSharpLanguage.CSharpLanguage.EventSink..ctor -> Old.CSharpLanguage.CSharpLanguage.IInterfaceWithEvent",
            "Old.CSharpLanguage.CSharpLanguage.EventSink..ctor -> Old.CSharpLanguage.CSharpLanguage.IInterfaceWithEvent.MyEvent",
            "Old.CSharpLanguage.CSharpLanguage.Extensions.Slice -> Old.CSharpLanguage.CSharpLanguage.TheExtendedType",
            "Old.CSharpLanguage.CSharpLanguage.NS_Parent.NS_Irrelevant.ClassNsIrrelevant._delegate2 -> Old.CSharpLanguage.CSharpLanguage.NS_Parent.NS_Child.ClassNsChild.DelegateInChild",
            "Old.CSharpLanguage.CSharpLanguage.NS_Parent.ClassINparent._delegate1 -> Old.CSharpLanguage.CSharpLanguage.NS_Parent.NS_Child.ClassNsChild.DelegateInChild",
            "Old.CSharpLanguage.CSharpLanguage.Partial.Client.CreateInstance -> Old.CSharpLanguage.CSharpLanguage.Partial.Client",
            "Old.CSharpLanguage.CSharpLanguage.PinSignalView.PinSignalViewAutomationPeer._owner -> Old.CSharpLanguage.CSharpLanguage.PinSignalView",
            "Old.CSharpLanguage.CSharpLanguage.PinSignalView.PinSignalViewAutomationPeer..ctor -> Old.CSharpLanguage.CSharpLanguage.PinSignalView",
            "Old.CSharpLanguage.CSharpLanguage.PinSignalView.PinSignalViewAutomationPeer..ctor -> Old.CSharpLanguage.CSharpLanguage.PinSignalView.PinSignalViewAutomationPeer._owner",
            "Old.CSharpLanguage.CSharpLanguage.RecordA -> Old.CSharpLanguage.CSharpLanguage.RecordA",
            "Old.CSharpLanguage.CSharpLanguage.RecordA._recordB -> Old.CSharpLanguage.CSharpLanguage.RecordB",
            "Old.CSharpLanguage.CSharpLanguage.RecordB -> Old.CSharpLanguage.CSharpLanguage.RecordB",
            "Old.CSharpLanguage.CSharpLanguage.RecordB._recordA -> Old.CSharpLanguage.CSharpLanguage.RecordA",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base._base -> Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave -> Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base._base",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Driver._adpater1 -> Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter1",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Driver._adpater2 -> Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter2",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Driver..ctor -> Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Driver._adpater1",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Driver._adpater1 -> Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter1",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Driver._adpater2 -> Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter2",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Driver..ctor -> Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Driver._adpater1"
        };

        CollectionAssert.AreEquivalent(expected, actual);
    }

    [Test]
    public void FindsAllMethodImplementations()
    {
        var graph = GetTestAssemblyGraph();
        var actual = GetAllMethodImplementations(graph);

        var expected = new HashSet<string>
        {
            "Old.CSharpLanguage.CSharpLanguage.MissingInterface.BaseStorage.Load -> Old.CSharpLanguage.CSharpLanguage.MissingInterface.IStorage.Load",
            "Old.CSharpLanguage.CSharpLanguage.StructWithInterface.Method -> Old.CSharpLanguage.CSharpLanguage.IStructInterface.Method",

            // Regression Hierarchies
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassBase.MethodA -> Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.InterfaceA.MethodA",
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived1.MethodB -> Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.InterfaceB.MethodB",
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived1.MethodFromInterfaceBase -> Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.InterfaceBase.MethodFromInterfaceBase",
            // Yes, even if it is abstract. It is still an implementation.
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived1.MethodC -> Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.InterfaceC.MethodC"
        };


        CollectionAssert.AreEquivalent(expected, actual);
    }


    [Test]
    public void FindsAllMethodOverrides()
    {
        var graph = GetTestAssemblyGraph();
        var actual = GetAllMethodOverrides(graph);

        var expected = new HashSet<string>
        {
            // Regression Hierarchies
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived2.MethodA -> Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassBase.MethodA",
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived2.MethodC -> Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived1.MethodC",
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived3.MethodB -> Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived1.MethodB",
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived4.MethodA -> Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived2.MethodA",

            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter1.AddToSlave -> Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter2.AddToSlave -> Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter1.AddToSlave -> Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base.AddToSlave",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter2.AddToSlave -> Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base.AddToSlave"
        };


        CollectionAssert.AreEquivalent(expected, actual);
    }


    [Test]
    public void FindsAllEventUsages()
    {
        var graph = GetTestAssemblyGraph();

        // Registration and un-registration
        var actual = GetAllEventRegistrations(graph);
        var expected = new HashSet<string>
        {
            "Old.CSharpLanguage.CSharpLanguage.ClassUsingAnEvent.Init -> Old.CSharpLanguage.CSharpLanguage.ClassOfferingAnEvent.MyEvent1",
            "Old.CSharpLanguage.CSharpLanguage.ClassUsingAnEvent.Init -> Old.CSharpLanguage.CSharpLanguage.ClassOfferingAnEvent.MyEvent2",
            "Old.CSharpLanguage.CSharpLanguage.EventSink..ctor -> Old.CSharpLanguage.CSharpLanguage.IInterfaceWithEvent.MyEvent"
        };


        CollectionAssert.AreEquivalent(expected, actual);
    }

    private static HashSet<string> GetAllEventRegistrations(CodeGraph graph)
    {
        var actual = graph.Nodes.Values
            .SelectMany(n => n.Relationships)
            .Where(d => d.Type == RelationshipType.Uses)
            .Select(d => (graph.Nodes[d.SourceId], graph.Nodes[d.TargetId]))
            .Where(t => t.Item2.ElementType == CodeElementType.Event)
            .Select(t => $"{t.Item1.FullName} -> {t.Item2.FullName}")
            .ToHashSet();
        return actual;
    }


    [Test]
    public void FindsAllEventInvocation()
    {
        var graph = GetTestAssemblyGraph();
        var actual = GetAllEventInvocations(graph);

        var expected = new HashSet<string>
        {
            "Old.CSharpLanguage.CSharpLanguage.ClassOfferingAnEvent.OnEvent -> Old.CSharpLanguage.CSharpLanguage.ClassOfferingAnEvent.MyEvent2",
            "Old.CSharpLanguage.CSharpLanguage.EventInvocation.Raise1 -> Old.CSharpLanguage.CSharpLanguage.EventInvocation.MyEvent",
            "Old.CSharpLanguage.CSharpLanguage.EventInvocation.Raise2 -> Old.CSharpLanguage.CSharpLanguage.EventInvocation.MyEvent",
            "Old.CSharpLanguage.CSharpLanguage.EventInvocation.Raise3 -> Old.CSharpLanguage.CSharpLanguage.EventInvocation.MyEvent"
        };


        CollectionAssert.AreEquivalent(expected, actual);
    }



    [Test]
    public void FindsAllEventImplementations()
    {
        var graph = GetTestAssemblyGraph();
        var actual = GetAllEventImplementations(graph);


        var expected = new HashSet<string>
        {
            "Old.CSharpLanguage.CSharpLanguage.EventInvocation.MyEvent -> Old.CSharpLanguage.CSharpLanguage.IInterfaceWithEvent.MyEvent"
        };


        CollectionAssert.AreEquivalent(expected, actual);
    }



    [Test]
    public void FindsAllEventHandlers()
    {
        var graph = GetTestAssemblyGraph();
        var actual = GetRelationshipsOfType(graph, RelationshipType.Handles);
        var expected = new HashSet<string>
        {
            "Old.CSharpLanguage.CSharpLanguage.ClassUsingAnEvent.MyEventHandler -> Old.CSharpLanguage.CSharpLanguage.ClassOfferingAnEvent.MyEvent1",
            "Old.CSharpLanguage.CSharpLanguage.ClassUsingAnEvent.MyEventHandler2 -> Old.CSharpLanguage.CSharpLanguage.ClassOfferingAnEvent.MyEvent2",
            "Old.CSharpLanguage.CSharpLanguage.EventSink.Handler -> Old.CSharpLanguage.CSharpLanguage.IInterfaceWithEvent.MyEvent"
        };


        CollectionAssert.AreEquivalent(expected, actual);
    }


    [Test]
    public void FindsAllInterfaceImplementations()
    {
        var graph = GetTestAssemblyGraph();
        var actual = GetAllInterfaceImplementations(graph);


        var expected = new HashSet<string>
        {
            "Old.CSharpLanguage.CSharpLanguage.MissingInterface.Storage -> Old.CSharpLanguage.CSharpLanguage.MissingInterface.IStorage",
            "Old.CSharpLanguage.CSharpLanguage.EventInvocation -> Old.CSharpLanguage.CSharpLanguage.IInterfaceWithEvent",

            // Regression Hierarchies
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.InterfaceC -> Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.InterfaceBase",
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassBase -> Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.InterfaceA",
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived1 -> Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.InterfaceB",
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived1 -> Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.InterfaceC"
        };


        CollectionAssert.AreEquivalent(expected, actual);
    }

    [Test]
    public void FindsAllInheritance()
    {
        var graph = GetTestAssemblyGraph();
        var actual = GetAllClassInheritance(graph);

        var expected = new HashSet<string>
        {
            "Old.CSharpLanguage.CSharpLanguage.ProjectFile -> Old.CSharpLanguage.CSharpLanguage.XmlFile",
            "Old.CSharpLanguage.CSharpLanguage.MissingInterface.Storage -> Old.CSharpLanguage.CSharpLanguage.MissingInterface.BaseStorage",

            // Regression Hierarchies
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived1 -> Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassBase",
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived2 -> Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived1",
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived3 -> Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived2",
            "Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived4 -> Old.CSharpLanguage.CSharpLanguage.Regression_Hierarchies.ClassDerived3",


            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter1 -> Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter2 -> Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls1.Base",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter1 -> Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base",
            "Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter2 -> Old.CSharpLanguage.CSharpLanguage.Regression_FollowIncomingCalls2.Base"
        };


        CollectionAssert.AreEquivalent(expected, actual);
    }


    [Test]
    public void FindsAllUsingBetweenClasses()
    {
        var graph = GetTestAssemblyGraph();
        var actual = graph.Nodes.Values
            .SelectMany(n => n.Relationships)
            .Where(d => d.Type == RelationshipType.Uses)
            .Select(d => (graph.Nodes[d.SourceId], graph.Nodes[d.TargetId]))
            .Where(t => t.Item1.ElementType == CodeElementType.Class &&
                        t.Item2.ElementType == CodeElementType.Class)
            .Select(t => $"{t.Item1.FullName} -> {t.Item2.FullName}")
            .ToList();


        var expected = new HashSet<string>
        {
            "Old.CSharpLanguage.CSharpLanguage.ProjectFile -> Old.CSharpLanguage.CSharpLanguage.Project"
        };


        CollectionAssert.AreEquivalent(expected, actual);
    }

    [Test]
    public void Find_Interface_Implementation_In_BaseClass()
    {
        var graph = GetTestAssemblyGraph();
        // This case is not covered yet
        var iStorage = graph.Nodes.Values.Single(n => n.Name == "IStorage");
        var baseStorage = graph.Nodes.Values.Single(n => n.Name == "BaseStorage");
        var storage = graph.Nodes.Values.Single(n => n.Name == "Storage");
        var iLoad = iStorage.Children.Single();
        var load = baseStorage.Children.Single();

        Assert.IsTrue(
            storage.Relationships.Any(d => d.TargetId == iStorage.Id && d.Type == RelationshipType.Implements));
        Assert.IsTrue(
            storage.Relationships.Any(d => d.TargetId == baseStorage.Id && d.Type == RelationshipType.Inherits));

        // Not detected!
        Assert.IsTrue(load.Relationships.Any(d => d.TargetId == iLoad.Id && d.Type == RelationshipType.Implements));
    }
}