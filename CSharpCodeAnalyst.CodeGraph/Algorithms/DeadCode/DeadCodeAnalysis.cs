using CSharpCodeAnalyst.CodeGraph.Declarations;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.CodeGraph.Algorithms.DeadCode;

/// <summary>
///     Finds code nobody references any more.
///     <para>
///         The rule is expressed over the subtree, not over the single element: an element is dead when no
///         relationship enters its subtree from the outside. That makes the obvious case work - a class
///         whose method is called from elsewhere is alive even though nothing names the class itself - and
///         it also stops a class from keeping itself alive: methods that only call each other are internal
///         to the subtree and prove nothing. Because a dead element implies a dead subtree, only the
///         topmost dead element of a subtree is reported.
///     </para>
///     <para>
///         Polymorphism is handled by propagating liveness instead of counting the edge as a reference.
///         "Implements" / "Overrides" point from the implementation to the contract, so an implementation
///         never has an incoming reference and a contract member always looks used. Both are wrong. We
///         therefore ignore those edges as references and instead push liveness the other way: a contract
///         member that is called keeps all its implementations (and their types) alive. A contract that is
///         never called dies together with its implementations, which is exactly the finding one wants.
///         Contracts from outside the analyzed code are the exception - we cannot see who calls them, so
///         the implementation is still reported, but with
///         <see cref="DeadCodeHint.ImplementsExternalContract" /> and the lowest confidence: the decision
///         stays visible instead of silently removing the row. The note deliberately does not extend to
///         the containing type: a class whose only "use" is implementing IDisposable is still dead code.
///     </para>
///     <para>
///         References are counted in two colours. An element nothing references at all is the obvious
///         finding; an element only <i>test</i> code references is the same statement about the production
///         code, and is reported with <see cref="DeadCodeHint.UsedOnlyByTests" />. Without that rule,
///         analyzing the tests along with the production code would <i>hide</i> dead code - exactly the
///         elements one would see by excluding the test projects. Test code is decided per type (see
///         <see cref="FindTestTypes" />), and a test type is never a finding itself: the runner calls
///         what carries the test attributes, so a fixture is alive by definition, and its helper members
///         and nested fakes are the fixture's own business. An unattributed helper class outside the
///         fixtures is reported like anything else - only the tests need it, it goes when they go.
///     </para>
///     <para>
///         The analysis reports exactly what nothing references <i>right now</i>. It does not chase the
///         consequences: code that is only kept alive by the code just reported stays out of the result.
///         That is a deliberate step back from an earlier cascading version, which multiplied every false
///         positive - one invisible XAML binding took seven further elements with it. Deleting a finding
///         and running the analysis again gives the same answer without stacking the uncertainty.
///     </para>
///     <para>
///         Every finding carries a <see cref="DeadCodeFinding.Confidence" />, and
///         <see cref="AccessLevel" /> is what makes the top level reachable: an element confined to its
///         type or assembly cannot be referenced from code we did not analyze, so "nothing references it"
///         and "nothing can reference it" coincide. A producer that supplies no visibility never reaches
///         that level - which is the honest answer, not a penalty.
///     </para>
///     <para>
///         Three cases are dropped instead of reported. A public property of a type marked as a
///         serialization target (see <see cref="IsSerializedProperty" />): a serializer reaches it by
///         reflection, so on such a type an unreferenced property is the rule rather than the exception.
///         The members only the runtime ever calls, static constructor and finalizer (see
///         <see cref="IsRuntimeInvoked" />): no code can reference them, so the row would be wrong on
///         every live type. And the test types: the runner calls what carries the test attributes, so a
///         row per fixture would be wrong just the same - which also means an unused helper inside a
///         fixture is not found; the whole test type is out of scope.
///     </para>
///     <para>
///         A single property accessor <i>is</i> reported. Rolling it up into the property used to halve the
///         output, but it answered a question nobody asked: "is this property used at all" instead of "is
///         anything reading it". A setter nothing calls is a real finding, and hiding it contradicts the
///         rest of this class, which reports and annotates rather than drops. Every property is split into
///         get_Prop/set_Prop accessors, so this is the only shape the graph has.
///     </para>
///     <para>
///         Limitations, by construction: references the parser cannot see (reflection, dependency
///         injection, serialization) look like dead code - see <see cref="DeadCodeHint" />. Dead cycles are
///         not found either: two elements that only reference each other keep each other alive, which needs
///         reachability from an explicit set of entry points rather than a cascade.
///     </para>
/// </summary>
public static class DeadCodeAnalysis
{
    /// <summary>
    ///     The notes that say "the caller is somewhere we cannot see". They are what pins a finding to the
    ///     lowest confidence: we already know we might be wrong about it.
    /// </summary>
    private const DeadCodeHint CallerOutsideTheGraph =
        DeadCodeHint.EntryPoint | DeadCodeHint.Attributed | DeadCodeHint.ImplementsExternalContract;

    /// <summary>
    ///     Attribute names (with and without the "Attribute" suffix) of the common test frameworks. A test
    ///     method is called by a runner, never from the code, so it always looks unreferenced. Matching is
    ///     case-sensitive: the framework names are exact, and a domain attribute that happens to be called
    ///     "test" must not turn its class into test code.
    /// </summary>
    private static readonly HashSet<string> TestAttributes = new(StringComparer.Ordinal)
    {
        "Test", "TestAttribute",
        "TestCase", "TestCaseAttribute",
        "TestCaseSource", "TestCaseSourceAttribute",
        "TestFixture", "TestFixtureAttribute",
        "SetUp", "SetUpAttribute",
        "TearDown", "TearDownAttribute",
        "OneTimeSetUp", "OneTimeSetUpAttribute",
        "OneTimeTearDown", "OneTimeTearDownAttribute",
        "Fact", "FactAttribute",
        "Theory", "TheoryAttribute",
        "TestMethod", "TestMethodAttribute",
        "DataTestMethod", "DataTestMethodAttribute",
        "TestClass", "TestClassAttribute",
        "TestInitialize", "TestInitializeAttribute",
        "TestCleanup", "TestCleanupAttribute",
        "ClassInitialize", "ClassInitializeAttribute",
        "ClassCleanup", "ClassCleanupAttribute",
        "Benchmark", "BenchmarkAttribute"
    };

    /// <summary>
    ///     Attribute names (with and without the "Attribute" suffix) that talk to the compiler, the
    ///     debugger or a code analyzer - not to a runtime that would find the element by reflection. They
    ///     say nothing about a caller, so they do not raise <see cref="DeadCodeHint.Attributed" />;
    ///     without this list a single [Obsolete] or [DebuggerDisplay] pushed a finding down to the lowest
    ///     confidence. Every attribute <i>not</i> listed here keeps the doubt - a custom or framework
    ///     attribute ([Export], [HttpGet], ...) usually means exactly that some framework drives the
    ///     element. [Obsolete] is the extreme case: it argues for deleting the element, not against it.
    /// </summary>
    private static readonly HashSet<string> ToolingAttributes = new(StringComparer.Ordinal)
    {
        "Obsolete", "ObsoleteAttribute",
        "Flags", "FlagsAttribute",
        "AttributeUsage", "AttributeUsageAttribute",
        "Conditional", "ConditionalAttribute",
        "MethodImpl", "MethodImplAttribute",
        "CLSCompliant", "CLSCompliantAttribute",
        "DebuggerDisplay", "DebuggerDisplayAttribute",
        "DebuggerStepThrough", "DebuggerStepThroughAttribute",
        "DebuggerHidden", "DebuggerHiddenAttribute",
        "DebuggerNonUserCode", "DebuggerNonUserCodeAttribute",
        "DebuggerBrowsable", "DebuggerBrowsableAttribute",
        "DebuggerTypeProxy", "DebuggerTypeProxyAttribute",
        "ExcludeFromCodeCoverage", "ExcludeFromCodeCoverageAttribute",
        "SuppressMessage", "SuppressMessageAttribute",
        "EditorBrowsable", "EditorBrowsableAttribute",
        "GeneratedCode", "GeneratedCodeAttribute"
    };

    /// <summary>
    ///     Attribute names (with and without the "Attribute" suffix) that mark a whole type as a
    ///     serialization target. A serializer reads and writes the public properties by reflection, so on
    ///     such a type they look unreferenced no matter how heavily the type is used.
    /// </summary>
    private static readonly HashSet<string> SerializationAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Serializable", "SerializableAttribute",
        "DataContract", "DataContractAttribute",
        "JsonObject", "JsonObjectAttribute",
        "JsonConverter", "JsonConverterAttribute",
        "XmlRoot", "XmlRootAttribute",
        "XmlType", "XmlTypeAttribute",
        "ProtoContract", "ProtoContractAttribute",
        "MessagePackObject", "MessagePackObjectAttribute"
    };

    /// <param name="externalContracts">
    ///     What the parser recorded beside the graph: which members implement or override something from
    ///     outside the analyzed code. Optional - without it those members are reported like any other
    ///     unreferenced member, which is what they look like from the graph alone.
    /// </param>
    public static List<DeadCodeFinding> Calculate(Graph.CodeGraph graph,
        ExternalContractStore? externalContracts = null)
    {
        ArgumentNullException.ThrowIfNull(graph);

        // Element -> the contract outside the analyzed code it implements. Two sources: the store the
        // parser fills from the symbols, and - when external code is part of the graph - the edges.
        var external = new Dictionary<string, string>();
        if (externalContracts is not null)
        {
            foreach (var (elementId, contract) in externalContracts.Contracts)
            {
                external[elementId] = contract;
            }
        }

        // Internal contract member -> the members implementing / overriding it, and the reverse.
        var implementations = new Dictionary<string, List<CodeElement>>();
        var contracts = new Dictionary<string, List<CodeElement>>();

        var referenceEdges = new List<(CodeElement Source, CodeElement Target)>();
        CollectEdges(graph, referenceEdges, external, implementations, contracts);

        var testTypes = FindTestTypes(graph);

        // The types whose public properties a XAML {Binding} may read - see IsBindable. Recorded by the
        // parser; an importer graph or a project file saved before the set existed simply has none.
        var bindingSources = externalContracts?.NotifyingTypes.ToHashSet() ?? [];

        var context = new AnalysisContext(external, implementations, contracts,
            bindingSources, FindSerializableTypes(graph),
            testTypes, CollectTestReferences(referenceEdges, testTypes));

        var references = ComputeReferenced(referenceEdges, implementations, testTypes);

        return Report(graph, references, context)
            .OrderBy(f => f.Element.FullName, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The structural facts of one run, all derived once from the graph.</summary>
    private sealed record AnalysisContext(
        Dictionary<string, string> External,
        Dictionary<string, List<CodeElement>> Implementations,
        Dictionary<string, List<CodeElement>> Contracts,
        HashSet<string> BindingSources,
        HashSet<string> SerializableTypes,
        HashSet<string> TestTypes,
        Dictionary<string, List<CodeElement>> TestReferences);

    /// <summary>
    ///     What a relationship reaches, once counting every relationship and once counting only those that
    ///     do not start in test code. An element in <see cref="All" /> but not in
    ///     <see cref="FromProduction" /> is used by tests and by nothing else.
    /// </summary>
    private sealed record ReferenceSets(HashSet<string> All, HashSet<string> FromProduction);

    /// <summary>
    ///     The types holding the tests: every type on the ancestor chain of an element carrying a known
    ///     test-framework attribute.
    ///     <para>
    ///         The chain, not just the attributed element: xUnit has no class-level attribute at all
    ///         (only the [Fact] methods carry one) and NUnit finds classes without [TestFixture] too, so
    ///         it is the subtree that marks a fixture. Marking every ancestor type also covers the fakes
    ///         and test-data classes nested inside one.
    ///     </para>
    ///     <para>
    ///         Deliberately the type and not the assembly (which this used to be). One embedded test
    ///         class poisoned its whole assembly, in both directions: every reference leaving the
    ///         assembly counted as a test reference, so code in <i>other</i> assemblies used from there
    ///         was falsely used-only-by-tests - and production code beside the embedded tests, used only
    ///         by them, was never found, because the whole assembly was exempt. The price of the type
    ///         granularity is the unattributed helper outside the fixtures (builders, fakes), which is
    ///         now reported as used-only-by-tests. That is accepted, and it is a true statement: the
    ///         helper goes when the tests go.
    ///     </para>
    /// </summary>
    private static HashSet<string> FindTestTypes(Graph.CodeGraph graph)
    {
        var testTypes = new HashSet<string>();

        foreach (var element in graph.Nodes.Values)
        {
            if (!element.Attributes.Any(TestAttributes.Contains))
            {
                continue;
            }

            for (var current = element; current is not null; current = current.Parent)
            {
                if (current.IsType())
                {
                    testTypes.Add(current.Id);
                }
            }
        }

        return testTypes;
    }

    /// <summary>Whether the element is a test type or sits inside one.</summary>
    private static bool IsTestCode(CodeElement element, HashSet<string> testTypes)
    {
        for (var current = element; current is not null; current = current.Parent)
        {
            if (testTypes.Contains(current.Id))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Element -&gt; the test elements referencing it, so a used-only-by-tests finding can name what has
    ///     to be deleted with it. Only the relationships starting in test code are walked.
    /// </summary>
    private static Dictionary<string, List<CodeElement>> CollectTestReferences(
        List<(CodeElement Source, CodeElement Target)> referenceEdges, HashSet<string> testTypes)
    {
        var testReferences = new Dictionary<string, List<CodeElement>>();

        // Reused across relationships, like in ComputeReferenced.
        var reached = new HashSet<string>();
        var sourceChain = new HashSet<string>();

        foreach (var (source, target) in referenceEdges)
        {
            if (!IsTestCode(source, testTypes))
            {
                continue;
            }

            reached.Clear();
            MarkReferenced(source, target, reached, sourceChain);

            foreach (var id in reached)
            {
                if (!testReferences.TryGetValue(id, out var callers))
                {
                    callers = [];
                    testReferences[id] = callers;
                }

                // The same test typically reaches an element over several relationships.
                if (!callers.Contains(source))
                {
                    callers.Add(source);
                }
            }
        }

        return testReferences;
    }

    /// <summary>
    ///     The types a serializer drives: everything carrying one of the
    ///     <see cref="SerializationAttributes" />. Unlike the binding sources this is not spread down the
    ///     inheritance edges - none of those attributes is inherited, a derived type has to carry its own.
    /// </summary>
    private static HashSet<string> FindSerializableTypes(Graph.CodeGraph graph)
    {
        return graph.Nodes.Values
            .Where(element => element.IsType() && element.Attributes.Any(SerializationAttributes.Contains))
            .Select(element => element.Id)
            .ToHashSet();
    }

    private static CodeElement? ContainingType(CodeElement? element)
    {
        var current = element;
        while (current is not null && !current.IsType())
        {
            current = current.Parent;
        }

        return current;
    }

    /// <summary>
    ///     Everything a relationship enters from the outside, plus what a used contract keeps alive - the
    ///     same walk twice, once over all relationships and once leaving out those that start in test code.
    ///     <para>
    ///         The contract propagation has to run per colour, not once at the end: the UI calling
    ///         <c>ICodeGraphExplorer.FindIncomingCalls</c> keeps the implementation alive for production,
    ///         a test calling it does not. Sharing one propagation would mark every implementation of a
    ///         contract that any test uses as production code.
    ///     </para>
    /// </summary>
    private static ReferenceSets ComputeReferenced(
        List<(CodeElement Source, CodeElement Target)> referenceEdges,
        Dictionary<string, List<CodeElement>> implementations,
        HashSet<string> testTypes)
    {
        return new ReferenceSets(
            Mark(referenceEdges, implementations, _ => true),
            Mark(referenceEdges, implementations, source => !IsTestCode(source, testTypes)));
    }

    private static HashSet<string> Mark(
        List<(CodeElement Source, CodeElement Target)> referenceEdges,
        Dictionary<string, List<CodeElement>> implementations,
        Func<CodeElement, bool> includeSource)
    {
        var referenced = new HashSet<string>();

        // Reused across relationships to keep the walk allocation free.
        var sourceChain = new HashSet<string>();

        foreach (var (source, target) in referenceEdges)
        {
            if (includeSource(source))
            {
                MarkReferenced(source, target, referenced, sourceChain);
            }
        }

        PropagateContractUsage(referenced, implementations);
        return referenced;
    }

    private static void CollectEdges(Graph.CodeGraph graph,
        List<(CodeElement Source, CodeElement Target)> referenceEdges,
        Dictionary<string, string> external,
        Dictionary<string, List<CodeElement>> implementations, Dictionary<string, List<CodeElement>> contracts)
    {
        foreach (var relationship in graph.GetAllRelationships())
        {
            var source = graph.TryGetCodeElement(relationship.SourceId);
            var target = graph.TryGetCodeElement(relationship.TargetId);
            if (source is null || target is null)
            {
                continue;
            }

            if (IsPolymorphicEdge(relationship.Type, source))
            {
                RecordPolymorphicEdge(source, target, external, implementations, contracts);
                continue;
            }

            // Containment, Bundled and Handles are not references. Handles (handler -> event) is the
            // callback wiring; the registration site itself produces the method group "Uses" edge that
            // keeps the handler alive, so nothing is lost by ignoring it here.
            if (!relationship.Type.IsDependency())
            {
                continue;
            }

            referenceEdges.Add((source, target));
        }
    }

    /// <summary>
    ///     A relationship keeps alive every element whose subtree it enters from the outside: walking up
    ///     from the target, that is everything below the lowest common ancestor with the source. The common
    ///     ancestor and everything above it contain the source as well, so for them the relationship is an
    ///     internal one and proves nothing.
    /// </summary>
    private static void MarkReferenced(CodeElement source, CodeElement target, HashSet<string> referenced,
        HashSet<string> sourceChain)
    {
        sourceChain.Clear();
        for (var current = source; current is not null; current = current.Parent)
        {
            sourceChain.Add(current.Id);
        }

        for (var current = target; current is not null; current = current.Parent)
        {
            if (sourceChain.Contains(current.Id))
            {
                break;
            }

            referenced.Add(current.Id);
        }
    }

    /// <summary>
    ///     Marks an element alive whose caller is not part of the graph (a contract call reaching all
    ///     implementations). Without a source there is no common ancestor, so the whole chain is alive.
    /// </summary>
    private static void MarkReferencedFromOutside(CodeElement element, HashSet<string> referenced)
    {
        for (var current = element; current is not null; current = current.Parent)
        {
            referenced.Add(current.Id);
        }
    }

    /// <summary>
    ///     "Implements" and "Overrides" starting at a member express polymorphism, not use. Starting at a
    ///     type ("class C : IFoo") the same relationship names the interface in C's declaration and is an
    ///     ordinary reference - which is why the source, not the target, decides.
    /// </summary>
    private static bool IsPolymorphicEdge(RelationshipType type, CodeElement source)
    {
        return (type is RelationshipType.Implements or RelationshipType.Overrides) && !source.IsType();
    }

    private static void RecordPolymorphicEdge(CodeElement source, CodeElement target,
        Dictionary<string, string> external,
        Dictionary<string, List<CodeElement>> implementations, Dictionary<string, List<CodeElement>> contracts)
    {
        if (target.IsExternal || target.IsType())
        {
            // Either a framework contract, or the parser's fallback to the containing type because it
            // could not resolve the exact base member (generic base methods). Both mean the caller is
            // invisible. Recorded on the member only - implementing IDisposable is not a use of the class,
            // so the class itself stays reportable as dead code.
            external.TryAdd(source.Id, target.FullName);
            return;
        }

        Add(implementations, target.Id, source);
        Add(contracts, source.Id, target);
    }

    /// <summary>
    ///     Pushes liveness from a used contract member to its implementations: calling IFoo.Bar calls every
    ///     implementation of Bar, so the implementations and the types holding them are alive. Transitive,
    ///     because an override can itself be overridden.
    ///     <para>
    ///         Deliberately <i>every</i> implementation, not only those whose type is visibly instantiated
    ///         (the class-hierarchy-analysis choice, not the rapid-type-analysis refinement). The
    ///         refinement would need a Creates edge to exist for every live type - but implementations of
    ///         an interface are exactly the classes a dependency container instantiates, and a container
    ///         registration produces no Creates edge (or, with assembly scanning, no edge at all). The
    ///         refinement would report precisely the code it cannot see being created.
    ///     </para>
    /// </summary>
    private static void PropagateContractUsage(HashSet<string> referenced,
        Dictionary<string, List<CodeElement>> implementations)
    {
        var queue = new Queue<string>(implementations.Keys.Where(referenced.Contains));
        var enqueued = new HashSet<string>(queue);

        while (queue.Count > 0)
        {
            foreach (var implementation in implementations[queue.Dequeue()])
            {
                MarkReferencedFromOutside(implementation, referenced);

                if (implementations.ContainsKey(implementation.Id) && enqueued.Add(implementation.Id))
                {
                    queue.Enqueue(implementation.Id);
                }
            }
        }
    }

    /// <summary>
    ///     Everything the production code does not reference, reduced to the topmost element of each dead
    ///     subtree.
    /// </summary>
    private static List<DeadCodeFinding> Report(Graph.CodeGraph graph, ReferenceSets references,
        AnalysisContext context)
    {
        var findings = new List<DeadCodeFinding>();

        foreach (var element in graph.Nodes.Values)
        {
            if (!IsReported(element, references, context))
            {
                continue;
            }

            if (IsSerializedProperty(element, context.SerializableTypes))
            {
                continue;
            }

            // Roll-up: a dead element inside a dead element is reported as part of it. Namespaces and
            // assemblies are no candidates, so an element directly below them is always the topmost one.
            if (element.Parent is not null && IsReported(element.Parent, references, context))
            {
                continue;
            }

            findings.Add(CreateFinding(element, references, context));
        }

        return findings;
    }

    /// <summary>
    ///     Whether nothing in the production code references the element. Two ways to get there: nothing
    ///     references it at all, or only test code does.
    ///     <para>
    ///         Test types are out of scope entirely: the runner calls what carries the test attributes,
    ///         so a fixture is alive by definition, and everything else inside it is the fixture's own
    ///         business.
    ///     </para>
    ///     <para>
    ///         An external contract does not make an element alive either; it is reported with a note
    ///         instead, so the decision stays visible rather than silently removing rows from the result.
    ///     </para>
    /// </summary>
    private static bool IsReported(CodeElement element, ReferenceSets references, AnalysisContext context)
    {
        if (!IsCandidate(element) || IsTestCode(element, context.TestTypes))
        {
            return false;
        }

        return !references.FromProduction.Contains(element.Id);
    }

    private static DeadCodeFinding CreateFinding(CodeElement element, ReferenceSets references,
        AnalysisContext context)
    {
        var hints = DeadCodeHint.None;
        var attributes = new SortedSet<string>(StringComparer.Ordinal);

        // The hints are collected over the whole subtree: what is reported is a dead class, but the
        // evidence that it may still be alive usually sits on its members ([Test] methods, Main, ...).
        foreach (var member in element.GetSubtreeIncludingSelf())
        {
            if (IsEntryPoint(member))
            {
                hints |= DeadCodeHint.EntryPoint;
            }

            // A test attribute cannot show up here: it would have made every containing type a test
            // type, and those are never reported.
            foreach (var attribute in member.Attributes)
            {
                if (!ToolingAttributes.Contains(attribute))
                {
                    hints |= DeadCodeHint.Attributed;
                }

                attributes.Add(attribute);
            }
        }

        var related = new List<CodeElement>();

        // Reported although it implements an internal contract means the contract is dead as well -
        // otherwise the propagation would have marked this element alive.
        if (context.Contracts.TryGetValue(element.Id, out var implemented))
        {
            hints |= DeadCodeHint.ImplementsDeadContract;
            related.AddRange(implemented);
        }

        if (context.Implementations.TryGetValue(element.Id, out var implementors))
        {
            hints |= DeadCodeHint.ContractNeverCalled;
            related.AddRange(implementors);
        }

        // Element level only. A dead class whose members implement IDisposable is still dead - saying
        // "might be used" about the class would be wrong, the note belongs to the member.
        context.External.TryGetValue(element.Id, out var externalContract);
        if (externalContract is not null)
        {
            hints |= DeadCodeHint.ImplementsExternalContract;
        }

        if (element.IsGenerated)
        {
            hints |= DeadCodeHint.Generated;
        }

        // Reported although something references it means that something was test code - IsReported has
        // already established that the production code does not.
        var testReferences = new List<CodeElement>();
        if (references.All.Contains(element.Id))
        {
            hints |= DeadCodeHint.UsedOnlyByTests;
            testReferences = context.TestReferences.GetValueOrDefault(element.Id, []);
        }

        return new DeadCodeFinding(element)
        {
            Confidence = RateConfidence(element, hints, context),
            Hints = hints,
            Attributes = attributes.ToList(),
            RelatedMembers = related,
            TestReferences = testReferences,
            ExternalContract = externalContract
        };
    }

    /// <summary>
    ///     Three rules, in order. A note about a caller outside the graph beats everything - we already
    ///     know the finding may be wrong. Otherwise visibility decides, capped at
    ///     <see cref="DeadCodeConfidence.Medium" /> for a used-only-by-tests finding.
    ///     <para>
    ///         The cap, not a fixed level: the ladder measures whether a caller we cannot see could exist,
    ///         and that question stays valid here - a public element could still be used from an assembly
    ///         outside the analysis. <see cref="DeadCodeConfidence.High" /> is the one level it must not
    ///         reach, because that level claims nothing <i>can</i> reference the element while something
    ///         demonstrably does. Whether a test alone justifies keeping it is a decision, not a
    ///         measurement.
    ///     </para>
    /// </summary>
    private static DeadCodeConfidence RateConfidence(CodeElement element, DeadCodeHint hints,
        AnalysisContext context)
    {
        if ((hints & CallerOutsideTheGraph) != DeadCodeHint.None)
        {
            return DeadCodeConfidence.Low;
        }

        if (IsConfinedToAnalyzedCode(element) && !IsBindable(element, context.BindingSources) &&
            !hints.HasFlag(DeadCodeHint.UsedOnlyByTests))
        {
            return DeadCodeConfidence.High;
        }

        return DeadCodeConfidence.Medium;
    }

    /// <summary>
    ///     Whether a XAML <c>{Binding}</c> could read this element without us seeing it - bindings are
    ///     resolved by reflection at runtime and are the one XAML construct the parser deliberately does
    ///     not follow. That takes two things: a <b>public</b> property - the binding engine resolves by
    ///     public reflection, so private, internal and protected members are out of its reach - on a type
    ///     that raises change notifications.
    ///     <para>
    ///         Which types those are comes straight from the parser
    ///         (<see cref="ExternalContractStore.NotifyingTypes" />): every analyzed type with
    ///         <c>INotifyPropertyChanged</c> anywhere in its interface set, no matter which class of the
    ///         inheritance chain implements it - so a view model deriving from a base class outside the
    ///         analyzed code (ObservableObject, BindableBase, ...) counts too, although from the graph
    ///         alone it is indistinguishable from any other class.
    ///     </para>
    ///     <para>
    ///         Note that being confined does not help here. A public property of an internal class cannot
    ///         be referenced from another assembly, but the binding sits <i>inside</i> the assembly and is
    ///         merely invisible, which is a different thing.
    ///     </para>
    /// </summary>
    private static bool IsBindable(CodeElement element, HashSet<string> bindingSources)
    {
        return IsPublicPropertyOf(element, bindingSources);
    }

    /// <summary>
    ///     Whether the element is a public property of a type marked as a serialization target. The
    ///     serializer reaches it by reflection, so "nothing references it" says nothing about it at all -
    ///     such a property is not reported.
    ///     <para>
    ///         Dropping it rather than reporting it with a note is deliberate: on a DTO <i>every</i>
    ///         property looks dead, so the note would be the rule rather than the exception and would fill
    ///         the result with rows nobody can act on.
    ///     </para>
    /// </summary>
    private static bool IsSerializedProperty(CodeElement element, HashSet<string> serializableTypes)
    {
        return IsPublicPropertyOf(element, serializableTypes);
    }

    /// <summary>
    ///     Whether the element is a public property of one of the given types - or a <b>public accessor</b>
    ///     of one, which is what a single dead getter or setter is reported as when the other accessor
    ///     keeps the property alive. The reflection-based reader goes through the accessor (a binding
    ///     read is a getter call, a deserializer writes the setter), so the rule has to hold on both
    ///     levels. An accessor narrowed below public ("public string X { get; private set; }") is out of
    ///     reflection's reach and does not count.
    /// </summary>
    private static bool IsPublicPropertyOf(CodeElement element, HashSet<string> types)
    {
        if (element is { ElementType: CodeElementType.PropertyAccessor, AccessLevel: AccessLevel.Public, Parent: not null })
        {
            return IsPublicPropertyOf(element.Parent, types);
        }

        if (element is not { ElementType: CodeElementType.Property, AccessLevel: AccessLevel.Public })
        {
            return false;
        }

        var type = ContainingType(element);
        return type is not null && types.Contains(type.Id);
    }

    /// <summary>
    ///     Whether the element is out of reach for code we did not analyze. It is enough that *any*
    ///     container is private or internal: a public method of an internal class cannot be called from
    ///     another assembly either. An element whose visibility is unknown contributes nothing, so a graph
    ///     from an importer that does not supply it never reaches high confidence.
    ///     <para>
    ///         "InternalsVisibleTo" is not considered. A friend assembly inside the analysis would show its
    ///         references anyway; one outside it is the rare case this misses.
    ///     </para>
    /// </summary>
    private static bool IsConfinedToAnalyzedCode(CodeElement element)
    {
        for (var current = element; current is not null; current = current.Parent)
        {
            if (current.AccessLevel.IsConfinedToAnalyzedCode())
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Containers never carry relationships of their own, so they would all look dead. External
    ///     elements are out of scope - we see neither their callers nor their bodies. And the members
    ///     only the runtime calls can never be dead on their own.
    /// </summary>
    private static bool IsCandidate(CodeElement element)
    {
        return !element.IsExternal &&
               element.ElementType is not (CodeElementType.Assembly or CodeElementType.Namespace) &&
               !IsRuntimeInvoked(element);
    }

    /// <summary>
    ///     Members only the runtime ever calls: the static constructor (run before the first use of the
    ///     type) and the finalizer or destructor (run by the garbage collector - C# cannot even spell a
    ///     call to it). No code can reference them, so "nothing references it" carries no information
    ///     here: on a live type the row would be wrong in every single case, and on a dead type they
    ///     disappear into the roll-up. Unlike an entry point this is not a doubt worth a note - the
    ///     finding is dropped. Both are effectively private, so without this they would land in the
    ///     highest confidence band.
    ///     <para>
    ///         The instance constructor is deliberately not here: it is called from ordinary code, so
    ///         "nothing constructs this" is a real finding.
    ///     </para>
    /// </summary>
    private static bool IsRuntimeInvoked(CodeElement element)
    {
        return element.MemberRole is MemberRole.StaticConstructor or MemberRole.Finalizer;
    }

    /// <summary>
    ///     Started from outside the analyzed code: the program entry point, and the synthetic
    ///     "GlobalStatements" class the parser creates per assembly for top-level statements.
    /// </summary>
    private static bool IsEntryPoint(CodeElement element)
    {
        if (element is { ElementType: CodeElementType.Method, Name: "Main" })
        {
            return true;
        }

        return element is { ElementType: CodeElementType.Class, Name: "GlobalStatements" } &&
               (element.Parent?.ElementType == CodeElementType.Assembly ||
                element.Parent is { ElementType: CodeElementType.Namespace, Name: CodeElement.GlobalNamespaceName });
    }

    private static void Add(Dictionary<string, List<CodeElement>> map, string key, CodeElement value)
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = [];
            map[key] = list;
        }

        list.Add(value);
    }
}
