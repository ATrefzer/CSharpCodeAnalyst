using CodeParserTests.Helper;
using CSharpCodeAnalyst.CodeGraph.Algorithms.DeadCode;
using CSharpCodeAnalyst.CodeGraph.Declarations;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CodeParserTests.UnitTests.DeadCode;

[TestFixture]
public class DeadCodeAnalysisTests
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

    private string[] Reported()
    {
        return DeadCodeAnalysis.Calculate(_graph)
            .Select(f => f.Element.FullName)
            .ToArray();
    }

    private DeadCodeFinding FindingFor(CodeElement element)
    {
        return DeadCodeAnalysis.Calculate(_graph).Single(f => f.Element.Id == element.Id);
    }

    [Test]
    public void Calculate_EmptyGraph_NoFindings()
    {
        Assert.That(DeadCodeAnalysis.Calculate(_graph), Is.Empty);
    }

    [Test]
    public void Calculate_UnreferencedClass_Reported()
    {
        _graph.CreateClass("A");

        Assert.That(Reported(), Is.EquivalentTo(new[] { "A" }));
    }

    [Test]
    public void Calculate_ClassUsedByAnotherClass_NotReported()
    {
        // B uses A, so only B itself has no incoming reference.
        var a = _graph.CreateClass("A");
        var b = _graph.CreateClass("B");
        Rel(b, a, RelationshipType.Uses);

        Assert.That(Reported(), Is.EquivalentTo(new[] { "B" }));
    }

    [Test]
    public void Calculate_MethodCalledFromOutside_KeepsWholeClassAlive()
    {
        // Nothing names A, but B.M calls A.M -> the reference enters A's subtree from the outside.
        var a = _graph.CreateClass("A");
        var am = _graph.CreateMethod("A.M", a);
        var b = _graph.CreateClass("B");
        var bm = _graph.CreateMethod("B.M", b);
        Rel(bm, am, RelationshipType.Calls);

        // B is dead; B.M is inside it and rolled up.
        Assert.That(Reported(), Is.EquivalentTo(new[] { "B" }));
    }

    [Test]
    public void Calculate_ClassWithOnlyInternalCalls_ReportedAsOneFinding()
    {
        // A.M1 -> A.M2 does not prove anything about A: the reference never leaves the subtree.
        var a = _graph.CreateClass("A");
        var m1 = _graph.CreateMethod("A.M1", a);
        var m2 = _graph.CreateMethod("A.M2", a);
        Rel(m1, m2, RelationshipType.Calls);

        Assert.That(Reported(), Is.EquivalentTo(new[] { "A" }));
    }

    [Test]
    public void Calculate_UnusedMemberOfLiveClass_Reported()
    {
        var a = _graph.CreateClass("A");
        var used = _graph.CreateMethod("A.Used", a);
        _graph.CreateMethod("A.Unused", a);
        var b = _graph.CreateClass("B");
        var bm = _graph.CreateMethod("B.M", b);
        Rel(bm, used, RelationshipType.Calls);

        Assert.That(Reported(), Is.EquivalentTo(new[] { "A.Unused", "B" }));
    }

    [Test]
    public void Calculate_NestedDeadClass_OnlyOutermostReported()
    {
        var outer = _graph.CreateClass("Outer");
        var inner = _graph.CreateClass("Outer.Inner", outer);
        _graph.CreateMethod("Outer.Inner.M", inner);

        Assert.That(Reported(), Is.EquivalentTo(new[] { "Outer" }));
    }

    [Test]
    public void Calculate_ContainersAndExternalElements_NeverReported()
    {
        var assembly = _graph.CreateAssembly("Asm");
        var ns = _graph.CreateNamespace("Asm.Ns", assembly);
        _graph.CreateClass("Asm.Ns.A", ns);
        _graph.CreateExternalClass("Ext");

        Assert.That(Reported(), Is.EquivalentTo(new[] { "Asm.Ns.A" }));
    }

    [Test]
    public void Calculate_ContainmentAndHandles_AreNoReferences()
    {
        // Handles points handler -> event; it is the callback wiring, not a use of the handler.
        var publisher = _graph.CreateClass("Publisher");
        var evt = _graph.CreateEvent("Publisher.Changed", publisher);
        var subscriber = _graph.CreateClass("Subscriber");
        var handler = _graph.CreateMethod("Subscriber.OnChanged", subscriber);
        Rel(handler, evt, RelationshipType.Handles);
        Rel(publisher, subscriber, RelationshipType.Containment);

        // The Handles edge enters Publisher's subtree and the Containment edge enters Subscriber's,
        // yet neither is a reference - both classes stay dead.
        Assert.That(Reported(), Is.EquivalentTo(new[] { "Publisher", "Subscriber" }));
    }

    [Test]
    public void Calculate_CalledThroughInterface_KeepsImplementationAndItsTypeAlive()
    {
        var contract = _graph.CreateInterface("IFoo");
        var contractMember = _graph.CreateMethod("IFoo.Bar", contract);
        var impl = _graph.CreateClass("C");
        var implMember = _graph.CreateMethod("C.Bar", impl);
        Rel(impl, contract, RelationshipType.Implements);
        Rel(implMember, contractMember, RelationshipType.Implements);

        var user = _graph.CreateClass("User");
        var userMethod = _graph.CreateMethod("User.M", user);
        Rel(userMethod, contractMember, RelationshipType.Calls);

        // Nobody creates C, yet the call through IFoo.Bar reaches C.Bar - so neither is dead.
        Assert.That(Reported(), Is.EquivalentTo(new[] { "User" }));
    }

    [Test]
    public void Calculate_OverrideChain_LivenessPropagatesTransitively()
    {
        var contract = _graph.CreateInterface("IFoo");
        var contractMember = _graph.CreateMethod("IFoo.Bar", contract);
        var middle = _graph.CreateClass("Base");
        var middleMember = _graph.CreateMethod("Base.Bar", middle);
        var leaf = _graph.CreateClass("Derived");
        var leafMember = _graph.CreateMethod("Derived.Bar", leaf);

        Rel(middle, contract, RelationshipType.Implements);
        Rel(middleMember, contractMember, RelationshipType.Implements);
        Rel(leaf, middle, RelationshipType.Inherits);
        Rel(leafMember, middleMember, RelationshipType.Overrides);

        var user = _graph.CreateClass("User");
        Rel(user, contractMember, RelationshipType.Calls);

        Assert.That(Reported(), Is.EquivalentTo(new[] { "User" }));
    }

    [Test]
    public void Calculate_ContractImplementedButNeverCalled_ContractAndImplementationReported()
    {
        var contract = _graph.CreateInterface("IFoo");
        var contractMember = _graph.CreateMethod("IFoo.Bar", contract);
        var impl = _graph.CreateClass("C");
        var implMember = _graph.CreateMethod("C.Bar", impl);
        Rel(impl, contract, RelationshipType.Implements);
        Rel(implMember, contractMember, RelationshipType.Implements);

        // C is instantiated, so the class itself is alive - but nobody ever calls Bar.
        var user = _graph.CreateClass("User");
        Rel(user, impl, RelationshipType.Creates);

        Assert.That(Reported(), Is.EquivalentTo(new[] { "C.Bar", "IFoo.Bar", "User" }));

        var contractFinding = FindingFor(contractMember);
        Assert.That(contractFinding.Hints.HasFlag(DeadCodeHint.ContractNeverCalled), Is.True);
        Assert.That(contractFinding.RelatedMembers.Select(m => m.FullName), Is.EquivalentTo(new[] { "C.Bar" }));

        var implFinding = FindingFor(implMember);
        Assert.That(implFinding.Hints.HasFlag(DeadCodeHint.ImplementsDeadContract), Is.True);
        Assert.That(implFinding.RelatedMembers.Select(m => m.FullName), Is.EquivalentTo(new[] { "IFoo.Bar" }));
    }

    [Test]
    public void Calculate_ImplementsExternalContract_MemberAliveButClassStillDead()
    {
        // class C : IDisposable { public void Dispose() {} } - Dispose is called by code we cannot see,
        // but implementing IDisposable is no use of C itself.
        var external = _graph.CreateExternalInterface("IDisposable");
        var externalMember = _graph.CreateExternalMethod("IDisposable.Dispose", external);
        var impl = _graph.CreateClass("C");
        var implMember = _graph.CreateMethod("C.Dispose", impl);
        Rel(impl, external, RelationshipType.Implements);
        Rel(implMember, externalMember, RelationshipType.Implements);

        Assert.That(Reported(), Is.EquivalentTo(new[] { "C" }));
    }

    [Test]
    public void Calculate_OverridesUnresolvedBaseMember_ReportedWithTheExternalContractHint()
    {
        // The parser falls back to the containing type when it cannot resolve the exact base member
        // (generic base methods). We cannot tell who calls it - the member is still reported, but the
        // note says why it is probably alive rather than dropping the row silently.
        var baseClass = _graph.CreateClass("Base");
        var derived = _graph.CreateClass("Derived");
        var member = _graph.CreateMethod("Derived.M", derived);
        Rel(derived, baseClass, RelationshipType.Inherits);
        Rel(member, baseClass, RelationshipType.Overrides);

        var user = _graph.CreateClass("User");
        Rel(user, derived, RelationshipType.Creates);

        Assert.That(Reported(), Is.EquivalentTo(new[] { "Derived.M", "User" }));

        var finding = FindingFor(member);
        Assert.Multiple(() =>
        {
            Assert.That(finding.Hints.HasFlag(DeadCodeHint.ImplementsExternalContract), Is.True);
            Assert.That(finding.ExternalContract, Is.EqualTo("Base"));
        });
    }

    [Test]
    public void Calculate_ExternalContractFromTheStore_ReportedWithTheContractName()
    {
        // The usual case: the parser recorded the contract beside the graph because there is no element
        // to point an edge at (IncludeExternals is off, so ICommand is not in the graph at all).
        var live = _graph.CreateClass("Command");
        var execute = _graph.CreateMethod("Command.Execute", live);
        var user = _graph.CreateClass("User");
        Rel(user, live, RelationshipType.Creates);

        var store = new ExternalContractStore();
        store.Add(execute.Id, "ICommand.Execute");

        var findings = DeadCodeAnalysis.Calculate(_graph, store);
        var finding = findings.Single(f => f.Element.Id == execute.Id);

        Assert.Multiple(() =>
        {
            Assert.That(finding.Hints.HasFlag(DeadCodeHint.ImplementsExternalContract), Is.True);
            Assert.That(finding.ExternalContract, Is.EqualTo("ICommand.Execute"));

            // The class itself is untouched by the assumption - it is created, so it is not reported.
            Assert.That(findings.Select(f => f.Element.FullName), Does.Not.Contain("Command"));
        });
    }

    [Test]
    public void Calculate_EntryPointAndAttributed_ReportedWithHint()
    {
        var program = _graph.CreateClass("Program");
        _graph.CreateMethod("Main", program);

        var service = _graph.CreateClass("Service");
        service.Attributes.Add("ExportAttribute");

        Assert.That(FindingFor(program).Hints, Is.EqualTo(DeadCodeHint.EntryPoint));
        Assert.That(FindingFor(service).Hints, Is.EqualTo(DeadCodeHint.Attributed));
        Assert.That(FindingFor(service).Attributes, Is.EquivalentTo(new[] { "ExportAttribute" }));
    }

    [Test]
    public void Calculate_ToolingAttributes_RaiseNoDoubt()
    {
        // [Obsolete] and the debugger attributes talk to the compiler or debugger, not to a runtime
        // that could call the element - they must not count as a caller doubt. The attributes are
        // still collected on the finding.
        var service = _graph.CreateClass("Service");
        service.Attributes.Add("ObsoleteAttribute");
        service.Attributes.Add("DebuggerDisplayAttribute");

        var finding = FindingFor(service);

        Assert.Multiple(() =>
        {
            Assert.That(finding.Hints, Is.EqualTo(DeadCodeHint.None));
            Assert.That(finding.Attributes,
                Is.EquivalentTo(new[] { "DebuggerDisplayAttribute", "ObsoleteAttribute" }));
        });
    }

    [Test]
    public void Calculate_StaticConstructorAndFinalizer_AreNeverReported()
    {
        // No code can reference either - the runtime calls them. On a live type the row would be wrong
        // in every case, so it is dropped rather than annotated; on a dead type the roll-up covers them.
        var cache = _graph.CreateClass("Cache");
        _graph.CreateMethod(".cctor", cache, memberRole: MemberRole.StaticConstructor);
        _graph.CreateMethod("Finalize", cache, memberRole: MemberRole.Finalizer);
        var used = _graph.CreateMethod("Cache.Used", cache);

        var user = _graph.CreateClass("User");
        Rel(user, used, RelationshipType.Calls);

        Assert.That(Reported(), Is.EquivalentTo(new[] { "User" }));
    }

    [Test]
    public void Calculate_DeadClassWithStaticConstructor_IsReportedWithoutAnEntryPointHint()
    {
        // An unused type's static constructor never runs. It must not push the class finding down to
        // the lowest confidence the way a real entry point would.
        var cache = _graph.CreateClass("Cache");
        _graph.CreateMethod(".cctor", cache, memberRole: MemberRole.StaticConstructor);

        Assert.That(FindingFor(cache).Hints, Is.EqualTo(DeadCodeHint.None));
    }

    [Test]
    public void Calculate_UnusedPropertyAccessor_Reported()
    {
        var a = _graph.CreateClass("A");
        var property = _graph.CreateProperty("A.Value", a);
        var getter = _graph.CreatePropertyAccessor("A.get_Value", property);
        _graph.CreatePropertyAccessor("A.set_Value", property);

        var user = _graph.CreateClass("User");
        Rel(user, getter, RelationshipType.Calls);

        // The property is used, so it is alive - but nothing writes it, and that is a finding of its own.
        Assert.That(Reported(), Is.EquivalentTo(new[] { "A.set_Value", "User" }));
    }

    [Test]
    public void Calculate_PropertyWithNoUsedAccessor_ReportedAsTheProperty()
    {
        var a = _graph.CreateClass("A");
        var property = _graph.CreateProperty("A.Value", a);
        _graph.CreatePropertyAccessor("A.get_Value", property);
        _graph.CreatePropertyAccessor("A.set_Value", property);

        var used = _graph.CreateMethod("A.Used", a);
        var user = _graph.CreateClass("User");
        Rel(user, used, RelationshipType.Calls);

        // A property that is dead as a whole is one finding, not three - the accessors roll up into it.
        Assert.That(Reported(), Is.EquivalentTo(new[] { "A.Value", "User" }));
    }

    [Test]
    public void Calculate_CodeOnlyUsedByDeadCode_NotReported()
    {
        // Deliberate: the analysis reports what nothing references right now and does not chase the
        // consequences. Formatter is referenced - by dead code, but referenced. Delete Report and run the
        // analysis again, and Formatter shows up. That keeps every finding standing on its own instead of
        // stacking on the round below it.
        var report = _graph.CreateClass("Report");
        var print = _graph.CreateMethod("Report.Print", report);
        var formatter = _graph.CreateClass("Formatter");
        var format = _graph.CreateMethod("Formatter.Format", formatter);
        Rel(print, format, RelationshipType.Calls);

        Assert.That(Reported(), Is.EquivalentTo(new[] { "Report" }));
    }

    [Test]
    public void Calculate_MutualReference_NotFound()
    {
        // The known limit: two elements that only reference each other keep each other alive. Finding
        // those needs reachability from an explicit set of entry points.
        var a = _graph.CreateClass("A");
        var am = _graph.CreateMethod("A.M", a);
        var b = _graph.CreateClass("B");
        var bm = _graph.CreateMethod("B.M", b);
        Rel(am, bm, RelationshipType.Calls);
        Rel(bm, am, RelationshipType.Calls);

        Assert.That(Reported(), Is.Empty);
    }

    /// <summary>
    ///     A type a serializer drives, kept alive by a user so that the members are reported individually.
    /// </summary>
    private CodeElement CreateSerializableType(string attribute = "DataContractAttribute")
    {
        var type = _graph.CreateClass("Config");
        type.Attributes.Add(attribute);

        var user = _graph.CreateClass("User");
        Rel(user, type, RelationshipType.Creates);
        return type;
    }

    [Test]
    public void Calculate_PublicPropertyOfASerializableType_NotReported()
    {
        // The serializer reads it by reflection. On such a type every property looks dead, so reporting
        // them would only fill the result with rows nobody can act on.
        var type = CreateSerializableType();
        _graph.CreateProperty("Config.Title", type, AccessLevel.Public);

        Assert.That(Reported(), Is.EquivalentTo(new[] { "User" }));
    }

    [Test]
    public void Calculate_NonPublicPropertyOfASerializableType_Reported()
    {
        // Out of reach for the serializer, which resolves by public reflection.
        var type = CreateSerializableType("SerializableAttribute");
        _graph.CreateProperty("Config.Secret", type, AccessLevel.Private);

        Assert.That(Reported(), Is.EquivalentTo(new[] { "Config.Secret", "User" }));
    }

    [Test]
    public void Calculate_MethodOfASerializableType_Reported()
    {
        // The exception is about the serialized state, not about everything on the type.
        var type = CreateSerializableType();
        _graph.CreateMethod("Config.Validate", type, AccessLevel.Public);

        Assert.That(Reported(), Is.EquivalentTo(new[] { "Config.Validate", "User" }));
    }

    [Test]
    public void Calculate_PublicPropertyOfAnOrdinaryType_Reported()
    {
        // Without one of the serialization attributes there is nothing to suspect.
        var type = _graph.CreateClass("Config");
        _graph.CreateProperty("Config.Title", type, AccessLevel.Public);

        var user = _graph.CreateClass("User");
        Rel(user, type, RelationshipType.Creates);

        Assert.That(Reported(), Is.EquivalentTo(new[] { "Config.Title", "User" }));
    }

    [Test]
    public void Calculate_UnusedSetterOfASerializedProperty_NotReported()
    {
        // The deserializer writes through the setter, so a dead setter on a DTO is the rule rather than
        // the exception - exactly like the property as a whole.
        var dto = _graph.CreateClass("Dto");
        dto.Attributes.Add("DataContract");
        var property = _graph.CreateProperty("Dto.Value", dto, AccessLevel.Public);
        var getter = _graph.CreatePropertyAccessor("Dto.get_Value", property, AccessLevel.Public);
        _graph.CreatePropertyAccessor("Dto.set_Value", property, AccessLevel.Public);

        var user = _graph.CreateClass("User");
        Rel(user, getter, RelationshipType.Calls);

        Assert.That(Reported(), Is.EquivalentTo(new[] { "User" }));
    }

    [Test]
    public void Calculate_PropertyOfASerializableTypeWithNoUsedAccessor_NotReported()
    {
        // Here the accessor roll-up alone would not help: nothing touches the property at all, so without
        // the serialization rule it would be reported as a dead property.
        var type = CreateSerializableType();
        var property = _graph.CreateProperty("Config.Title", type, AccessLevel.Public);
        _graph.CreatePropertyAccessor("Config.get_Title", property);
        _graph.CreatePropertyAccessor("Config.set_Title", property);

        Assert.That(Reported(), Is.EquivalentTo(new[] { "User" }));
    }
}
