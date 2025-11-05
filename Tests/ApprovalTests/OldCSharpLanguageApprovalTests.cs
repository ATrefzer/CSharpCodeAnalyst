using CodeGraph.Graph;

// ReSharper disable StringLiteralTypo

namespace CodeParserTests.ApprovalTests;

/// <summary>
///     Old legacy tests
/// </summary>
public class OldCSharpLanguageApprovalTests : ApprovalTestBase
{
    private CodeGraph.Graph.CodeGraph GetTestAssemblyGraph()
    {
        return GetTestGraph("Old.CSharpLanguage");
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
            "Old.CSharpLanguage.global.CSharpLanguage.ClassOfferingAnEvent.OnEvent",
            "Old.CSharpLanguage.global.CSharpLanguage.ClassUsingAnEvent.Init",
            "Old.CSharpLanguage.global.CSharpLanguage.ClassUsingAnEvent.MyEventHandler2",
            "Old.CSharpLanguage.global.CSharpLanguage.ClassUsingAnEvent.MyEventHandler",
            "Old.CSharpLanguage.global.CSharpLanguage.CreatorOfGenericTypes.Create",
            "Old.CSharpLanguage.global.CSharpLanguage.PinSignalView.OnCreteAutomationPeer",
            "Old.CSharpLanguage.global.CSharpLanguage.PinSignalView.PinSignalViewAutomationPeer..ctor",
            "Old.CSharpLanguage.global.CSharpLanguage.MissingInterface.BaseStorage.Load",
            "Old.CSharpLanguage.global.CSharpLanguage.MissingInterface.IStorage.Load",
            "Old.CSharpLanguage.global.CSharpLanguage.NS_Parent.NS_Child.ClassNsChild.Method",

            "Old.CSharpLanguage.global.CSharpLanguage.IStructInterface.Method",
            "Old.CSharpLanguage.global.CSharpLanguage.StructWithInterface.Method",

            // Extension method
            "Old.CSharpLanguage.global.CSharpLanguage.Extensions.Slice",

            // Called by extension method
            "Old.CSharpLanguage.global.CSharpLanguage.TheExtendedType.Do",

            // Generic method calls
            "Old.CSharpLanguage.global.CSharpLanguage.MoreGenerics.M1",
            "Old.CSharpLanguage.global.CSharpLanguage.MoreGenerics.M1",
            "Old.CSharpLanguage.global.CSharpLanguage.MoreGenerics.M2",
            "Old.CSharpLanguage.global.CSharpLanguage.MoreGenerics.M2",
            "Old.CSharpLanguage.global.CSharpLanguage.MoreGenerics.Run",


            // Show event registration
            "Old.CSharpLanguage.global.CSharpLanguage.EventInvocation.DoSomething",
            "Old.CSharpLanguage.global.CSharpLanguage.EventInvocation.Raise1",
            "Old.CSharpLanguage.global.CSharpLanguage.EventInvocation.Raise2",
            "Old.CSharpLanguage.global.CSharpLanguage.EventInvocation.Raise3",
            "Old.CSharpLanguage.global.CSharpLanguage.EventSink..ctor",
            "Old.CSharpLanguage.global.CSharpLanguage.EventSink.Handler",

            "Old.CSharpLanguage.global.CSharpLanguage.Partial.Client.CreateInstance",
            "Old.CSharpLanguage.global.CSharpLanguage.Partial.PartialClass.MethodInPartialClassPart1",
            "Old.CSharpLanguage.global.CSharpLanguage.Partial.PartialClass.MethodInPartialClassPart2",

            // Regression Hierarchies
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.InterfaceBase.MethodFromInterfaceBase",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.InterfaceA.MethodA",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.InterfaceB.MethodB",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.InterfaceC.MethodC",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassBase.MethodA",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived1.MethodB",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived1.MethodC",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived1.MethodFromInterfaceBase",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived2.MethodA",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived2.MethodC",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived3.MethodB",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived4.MethodA",

            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.Build",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Driver..ctor",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter1.AddToSlave",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter2.AddToSlave",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Base.AddToSlave",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Base.Build",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Driver..ctor",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter1.AddToSlave",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter2.AddToSlave"
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
            "Old.CSharpLanguage.global.CSharpLanguage.MoreGenerics.Foo",
            "Old.CSharpLanguage.global.CSharpLanguage.MoreGenerics.Foo",


            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived3",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived4",
            "Old.CSharpLanguage.global.CSharpLanguage.TheExtendedType",
            "Old.CSharpLanguage.global.CSharpLanguage.XmlFile",

            "Old.CSharpLanguage.global.CSharpLanguage.Partial.Client",
            "Old.CSharpLanguage.global.CSharpLanguage.Partial.PartialClass",
            "Old.CSharpLanguage.global.CSharpLanguage.PinSignalView",
            "Old.CSharpLanguage.global.CSharpLanguage.PinSignalView.PinSignalViewAutomationPeer",
            "Old.CSharpLanguage.global.CSharpLanguage.Project",
            "Old.CSharpLanguage.global.CSharpLanguage.ProjectFile",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassBase",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived1",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived2",

            "Old.CSharpLanguage.global.CSharpLanguage.My2Attribute",
            "Old.CSharpLanguage.global.CSharpLanguage.MyAttribute",
            "Old.CSharpLanguage.global.CSharpLanguage.MyEventArgs",
            "Old.CSharpLanguage.global.CSharpLanguage.NS_Parent.ClassINparent",
            "Old.CSharpLanguage.global.CSharpLanguage.NS_Parent.NS_Child.ClassNsChild",
            "Old.CSharpLanguage.global.CSharpLanguage.NS_Parent.NS_Irrelevant.ClassNsIrrelevant",
            "Old.CSharpLanguage.global.CSharpLanguage.Ns1.ClassM",
            "Old.CSharpLanguage.global.CSharpLanguage.Ns1.Ns2.ClassX",
            "Old.CSharpLanguage.global.CSharpLanguage.ClassOfferingAnEvent",
            "Old.CSharpLanguage.global.CSharpLanguage.ClassUsingAnEvent",
            "Old.CSharpLanguage.global.CSharpLanguage.CreatorOfGenericTypes",
            "Old.CSharpLanguage.global.CSharpLanguage.CustomException",
            "Old.CSharpLanguage.global.CSharpLanguage.EventInvocation",
            "Old.CSharpLanguage.global.CSharpLanguage.EventSink",
            "Old.CSharpLanguage.global.CSharpLanguage.Extensions",
            "Old.CSharpLanguage.global.CSharpLanguage.MissingInterface.BaseStorage",
            "Old.CSharpLanguage.global.CSharpLanguage.MissingInterface.Storage",
            "Old.CSharpLanguage.global.CSharpLanguage.MoreGenerics",

            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Driver",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter1",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter2",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Base",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Driver",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter1",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter2"
        };

        CollectionAssert.AreEquivalent(expected, actual);
    }

    [Test]
    public void FindsAllCalls()
    {
        var actual = GetRelationshipsOfType(GetTestAssemblyGraph(), RelationshipType.Calls);

        var expected = new HashSet<string>
        {
            "Old.CSharpLanguage.global.CSharpLanguage.ClassUsingAnEvent.Init -> Old.CSharpLanguage.global.CSharpLanguage.Extensions.Slice",
            "Old.CSharpLanguage.global.CSharpLanguage.Extensions.Slice -> Old.CSharpLanguage.global.CSharpLanguage.TheExtendedType.Do",
            "Old.CSharpLanguage.global.CSharpLanguage.MoreGenerics.Run -> Old.CSharpLanguage.global.CSharpLanguage.MoreGenerics.M1",
            "Old.CSharpLanguage.global.CSharpLanguage.MoreGenerics.Run -> Old.CSharpLanguage.global.CSharpLanguage.MoreGenerics.M2",

            "Old.CSharpLanguage.global.CSharpLanguage.EventInvocation.DoSomething -> Old.CSharpLanguage.global.CSharpLanguage.EventInvocation.Raise1",
            "Old.CSharpLanguage.global.CSharpLanguage.EventInvocation.DoSomething -> Old.CSharpLanguage.global.CSharpLanguage.EventInvocation.Raise2",
            "Old.CSharpLanguage.global.CSharpLanguage.EventInvocation.DoSomething -> Old.CSharpLanguage.global.CSharpLanguage.EventInvocation.Raise3",

            "Old.CSharpLanguage.global.CSharpLanguage.Partial.Client.CreateInstance -> Old.CSharpLanguage.global.CSharpLanguage.Partial.PartialClass.MethodInPartialClassPart1",
            "Old.CSharpLanguage.global.CSharpLanguage.Partial.Client.CreateInstance -> Old.CSharpLanguage.global.CSharpLanguage.Partial.PartialClass.MethodInPartialClassPart2",


            // Hierarchies. Calling the base classes.
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived2.MethodA -> Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassBase.MethodA",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived4.MethodA -> Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived2.MethodA",


            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave -> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.Build -> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Driver..ctor -> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.Build",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter1.AddToSlave -> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter2.AddToSlave -> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Base.Build -> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Base.AddToSlave",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Driver..ctor -> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Base.Build",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter1.AddToSlave -> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Base.AddToSlave",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter2.AddToSlave -> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Base.AddToSlave",

            "Old.CSharpLanguage.global.CSharpLanguage.PinSignalView.OnCreteAutomationPeer -> Old.CSharpLanguage.global.CSharpLanguage.PinSignalView.PinSignalViewAutomationPeer..ctor"
        };

        CollectionAssert.AreEquivalent(expected, actual);
    }

    [Test]
    public void FindsAllUses()
    {
        var actual = GetRelationshipsOfType(GetTestAssemblyGraph(), RelationshipType.Uses);
        var expected = new HashSet<string>
        {
            "Old.CSharpLanguage.global.CSharpLanguage.MyDelegate -> Old.CSharpLanguage.global.CSharpLanguage.MyEventArgs",
            "Old.CSharpLanguage.global.CSharpLanguage.ClassOfferingAnEvent.MyEvent1 -> Old.CSharpLanguage.global.CSharpLanguage.MyDelegate",
            "Old.CSharpLanguage.global.CSharpLanguage.ClassUsingAnEvent.Init -> Old.CSharpLanguage.global.CSharpLanguage.ClassOfferingAnEvent.MyEvent1",
            "Old.CSharpLanguage.global.CSharpLanguage.ClassUsingAnEvent.Init -> Old.CSharpLanguage.global.CSharpLanguage.ClassOfferingAnEvent.MyEvent2",
            "Old.CSharpLanguage.global.CSharpLanguage.ClassUsingAnEvent.MyEventHandler -> Old.CSharpLanguage.global.CSharpLanguage.MyEventArgs",
            "Old.CSharpLanguage.global.CSharpLanguage.ProjectFile -> Old.CSharpLanguage.global.CSharpLanguage.Project",
            "Old.CSharpLanguage.global.CSharpLanguage.CreatorOfGenericTypes._file -> Old.CSharpLanguage.global.CSharpLanguage.ProjectFile",
            "Old.CSharpLanguage.global.CSharpLanguage.CreatorOfGenericTypes.Create -> Old.CSharpLanguage.global.CSharpLanguage.Project",
            "Old.CSharpLanguage.global.CSharpLanguage.EventSink..ctor -> Old.CSharpLanguage.global.CSharpLanguage.IInterfaceWithEvent",
            "Old.CSharpLanguage.global.CSharpLanguage.EventSink..ctor -> Old.CSharpLanguage.global.CSharpLanguage.IInterfaceWithEvent.MyEvent",
            "Old.CSharpLanguage.global.CSharpLanguage.Extensions.Slice -> Old.CSharpLanguage.global.CSharpLanguage.TheExtendedType",
            "Old.CSharpLanguage.global.CSharpLanguage.NS_Parent.NS_Irrelevant.ClassNsIrrelevant._delegate2 -> Old.CSharpLanguage.global.CSharpLanguage.NS_Parent.NS_Child.ClassNsChild.DelegateInChild",
            "Old.CSharpLanguage.global.CSharpLanguage.NS_Parent.ClassINparent._delegate1 -> Old.CSharpLanguage.global.CSharpLanguage.NS_Parent.NS_Child.ClassNsChild.DelegateInChild",
            "Old.CSharpLanguage.global.CSharpLanguage.Partial.Client.CreateInstance -> Old.CSharpLanguage.global.CSharpLanguage.Partial.Client",
            "Old.CSharpLanguage.global.CSharpLanguage.PinSignalView.PinSignalViewAutomationPeer._owner -> Old.CSharpLanguage.global.CSharpLanguage.PinSignalView",
            "Old.CSharpLanguage.global.CSharpLanguage.PinSignalView.PinSignalViewAutomationPeer..ctor -> Old.CSharpLanguage.global.CSharpLanguage.PinSignalView",
            "Old.CSharpLanguage.global.CSharpLanguage.PinSignalView.PinSignalViewAutomationPeer..ctor -> Old.CSharpLanguage.global.CSharpLanguage.PinSignalView.PinSignalViewAutomationPeer._owner",
            "Old.CSharpLanguage.global.CSharpLanguage.RecordA -> Old.CSharpLanguage.global.CSharpLanguage.RecordA",
            "Old.CSharpLanguage.global.CSharpLanguage.RecordA._recordB -> Old.CSharpLanguage.global.CSharpLanguage.RecordB",
            "Old.CSharpLanguage.global.CSharpLanguage.RecordB -> Old.CSharpLanguage.global.CSharpLanguage.RecordB",
            "Old.CSharpLanguage.global.CSharpLanguage.RecordB._recordA -> Old.CSharpLanguage.global.CSharpLanguage.RecordA",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base._base -> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave -> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base._base",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Driver._adpater1 -> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter1",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Driver._adpater2 -> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter2",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Driver..ctor -> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Driver._adpater1",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Driver._adpater1 -> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter1",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Driver._adpater2 -> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter2",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Driver..ctor -> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Driver._adpater1",


            "Old.CSharpLanguage.global.CSharpLanguage.ClassUsingAnEvent.Init -> Old.CSharpLanguage.global.CSharpLanguage.ClassOfferingAnEvent",
            "Old.CSharpLanguage.global.CSharpLanguage.ClassUsingAnEvent.Init -> Old.CSharpLanguage.global.CSharpLanguage.TheExtendedType",
            "Old.CSharpLanguage.global.CSharpLanguage.CreatorOfGenericTypes.Create -> Old.CSharpLanguage.global.CSharpLanguage.XmlFile",
            "Old.CSharpLanguage.global.CSharpLanguage.MoreGenerics.Run -> Old.CSharpLanguage.global.CSharpLanguage.MoreGenerics.Foo",
            "Old.CSharpLanguage.global.CSharpLanguage.Partial.Client.CreateInstance -> Old.CSharpLanguage.global.CSharpLanguage.Partial.PartialClass",
            "Old.CSharpLanguage.global.CSharpLanguage.PinSignalView.OnCreteAutomationPeer -> Old.CSharpLanguage.global.CSharpLanguage.PinSignalView.PinSignalViewAutomationPeer",

            // Because of the event in AnalyzeIdentifier
            "Old.CSharpLanguage.global.CSharpLanguage.ClassOfferingAnEvent.OnEvent -> Old.CSharpLanguage.global.CSharpLanguage.ClassOfferingAnEvent.MyEvent2",
            "Old.CSharpLanguage.global.CSharpLanguage.EventInvocation.Raise1 -> Old.CSharpLanguage.global.CSharpLanguage.EventInvocation.MyEvent",
            "Old.CSharpLanguage.global.CSharpLanguage.EventInvocation.Raise2 -> Old.CSharpLanguage.global.CSharpLanguage.EventInvocation.MyEvent",
            "Old.CSharpLanguage.global.CSharpLanguage.EventInvocation.Raise3 -> Old.CSharpLanguage.global.CSharpLanguage.EventInvocation.MyEvent"
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
            "Old.CSharpLanguage.global.CSharpLanguage.MissingInterface.BaseStorage.Load -> Old.CSharpLanguage.global.CSharpLanguage.MissingInterface.IStorage.Load",
            "Old.CSharpLanguage.global.CSharpLanguage.StructWithInterface.Method -> Old.CSharpLanguage.global.CSharpLanguage.IStructInterface.Method",

            // Regression Hierarchies
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassBase.MethodA -> Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.InterfaceA.MethodA",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived1.MethodB -> Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.InterfaceB.MethodB",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived1.MethodFromInterfaceBase -> Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.InterfaceBase.MethodFromInterfaceBase",
            // Yes, even if it is abstract. It is still an implementation.
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived1.MethodC -> Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.InterfaceC.MethodC"
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
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived2.MethodA -> Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassBase.MethodA",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived2.MethodC -> Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived1.MethodC",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived3.MethodB -> Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived1.MethodB",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived4.MethodA -> Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived2.MethodA",

            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter1.AddToSlave -> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter2.AddToSlave -> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base.AddToSlave",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter1.AddToSlave -> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Base.AddToSlave",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter2.AddToSlave -> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Base.AddToSlave"
        };


        CollectionAssert.AreEquivalent(expected, actual);
    }


    [Test]
    public void FindsAllEventUsages()
    {
        var graph = GetTestAssemblyGraph();

        // Registration and un-registration
        var actual = GetAllEventUsages(graph);
        var expected = new HashSet<string>
        {
            "Old.CSharpLanguage.global.CSharpLanguage.ClassUsingAnEvent.Init -> Old.CSharpLanguage.global.CSharpLanguage.ClassOfferingAnEvent.MyEvent1",
            "Old.CSharpLanguage.global.CSharpLanguage.ClassUsingAnEvent.Init -> Old.CSharpLanguage.global.CSharpLanguage.ClassOfferingAnEvent.MyEvent2",
            "Old.CSharpLanguage.global.CSharpLanguage.EventSink..ctor -> Old.CSharpLanguage.global.CSharpLanguage.IInterfaceWithEvent.MyEvent",


            "Old.CSharpLanguage.global.CSharpLanguage.ClassOfferingAnEvent.OnEvent -> Old.CSharpLanguage.global.CSharpLanguage.ClassOfferingAnEvent.MyEvent2",
            "Old.CSharpLanguage.global.CSharpLanguage.EventInvocation.Raise1 -> Old.CSharpLanguage.global.CSharpLanguage.EventInvocation.MyEvent",
            "Old.CSharpLanguage.global.CSharpLanguage.EventInvocation.Raise2 -> Old.CSharpLanguage.global.CSharpLanguage.EventInvocation.MyEvent",
            "Old.CSharpLanguage.global.CSharpLanguage.EventInvocation.Raise3 -> Old.CSharpLanguage.global.CSharpLanguage.EventInvocation.MyEvent"
        };


        CollectionAssert.AreEquivalent(expected, actual);
    }

    private static HashSet<string> GetAllEventUsages(CodeGraph.Graph.CodeGraph graph)
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
            "Old.CSharpLanguage.global.CSharpLanguage.ClassOfferingAnEvent.OnEvent -> Old.CSharpLanguage.global.CSharpLanguage.ClassOfferingAnEvent.MyEvent2",
            "Old.CSharpLanguage.global.CSharpLanguage.EventInvocation.Raise1 -> Old.CSharpLanguage.global.CSharpLanguage.EventInvocation.MyEvent",
            "Old.CSharpLanguage.global.CSharpLanguage.EventInvocation.Raise2 -> Old.CSharpLanguage.global.CSharpLanguage.EventInvocation.MyEvent",
            "Old.CSharpLanguage.global.CSharpLanguage.EventInvocation.Raise3 -> Old.CSharpLanguage.global.CSharpLanguage.EventInvocation.MyEvent"
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
            "Old.CSharpLanguage.global.CSharpLanguage.EventInvocation.MyEvent -> Old.CSharpLanguage.global.CSharpLanguage.IInterfaceWithEvent.MyEvent"
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
            "Old.CSharpLanguage.global.CSharpLanguage.ClassUsingAnEvent.MyEventHandler -> Old.CSharpLanguage.global.CSharpLanguage.ClassOfferingAnEvent.MyEvent1",
            "Old.CSharpLanguage.global.CSharpLanguage.ClassUsingAnEvent.MyEventHandler2 -> Old.CSharpLanguage.global.CSharpLanguage.ClassOfferingAnEvent.MyEvent2",
            "Old.CSharpLanguage.global.CSharpLanguage.EventSink.Handler -> Old.CSharpLanguage.global.CSharpLanguage.IInterfaceWithEvent.MyEvent"
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
            "Old.CSharpLanguage.global.CSharpLanguage.MissingInterface.Storage -> Old.CSharpLanguage.global.CSharpLanguage.MissingInterface.IStorage",
            "Old.CSharpLanguage.global.CSharpLanguage.EventInvocation -> Old.CSharpLanguage.global.CSharpLanguage.IInterfaceWithEvent",

            // Regression Hierarchies
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.InterfaceC -> Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.InterfaceBase",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassBase -> Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.InterfaceA",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived1 -> Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.InterfaceB",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived1 -> Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.InterfaceC"
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
            "Old.CSharpLanguage.global.CSharpLanguage.ProjectFile -> Old.CSharpLanguage.global.CSharpLanguage.XmlFile",
            "Old.CSharpLanguage.global.CSharpLanguage.MissingInterface.Storage -> Old.CSharpLanguage.global.CSharpLanguage.MissingInterface.BaseStorage",

            // Regression Hierarchies
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived1 -> Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassBase",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived2 -> Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived1",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived3 -> Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived2",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived4 -> Old.CSharpLanguage.global.CSharpLanguage.Regression_Hierarchies.ClassDerived3",


            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter1 -> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.ViewModelAdapter2 -> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls1.Base",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter1 -> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Base",
            "Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.ViewModelAdapter2 -> Old.CSharpLanguage.global.CSharpLanguage.Regression_FollowIncomingCalls2.Base"
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
            "Old.CSharpLanguage.global.CSharpLanguage.ProjectFile -> Old.CSharpLanguage.global.CSharpLanguage.Project"
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