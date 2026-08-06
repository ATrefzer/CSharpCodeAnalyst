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
///         the implementation is assumed alive. That assumption deliberately does not extend to the
///         containing type: a class whose only "use" is implementing IDisposable is still dead code.
///     </para>
///     <para>
///         References are counted in two colours. An element nothing references at all is the obvious
///         finding; an element only <i>test</i> code references is the same statement about the production
///         code, and is reported with <see cref="DeadCodeHint.UsedOnlyByTests" />. Without that rule,
///         analyzing the tests along with the production code would <i>hide</i> dead code - exactly the
///         elements one would see by excluding the test projects. Test code is decided per assembly (see
///         <see cref="FindTestAssemblies" />), and inside a test assembly the rule is off: being used by
///         tests is what a test helper is for.
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
///         Two cases are dropped instead of reported: a single property accessor (see
///         <see cref="Report" />) and a public property of a type marked as a serialization target (see
///         <see cref="IsSerializedProperty" />). Both are the same kind of noise - a getter or setter that
///         only a serializer, a binding or a framework ever touches - and on the affected types they would
///         be the rule rather than the exception.
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
        DeadCodeHint.EntryPoint | DeadCodeHint.TestCode | DeadCodeHint.Attributed |
        DeadCodeHint.ImplementsExternalContract;

    /// <summary>Prefix of the contracts recorded for a type that raises change notifications.</summary>
    private const string NotifyPropertyChanged = "INotifyPropertyChanged.";

    /// <summary>
    ///     Attribute names (with and without the "Attribute" suffix) of the common test frameworks. A test
    ///     method is called by a runner, never from the code, so it always looks unreferenced.
    /// </summary>
    private static readonly HashSet<string> TestAttributes = new(StringComparer.OrdinalIgnoreCase)
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

        // Base type -> the types deriving from it. Only used to spread the binding target property.
        var derivedTypes = new Dictionary<string, List<CodeElement>>();

        var referenceEdges = new List<(CodeElement Source, CodeElement Target)>();
        CollectEdges(graph, referenceEdges, external, implementations, contracts, derivedTypes);

        var testAssemblies = FindTestAssemblies(graph);

        var context = new AnalysisContext(external, implementations, contracts,
            FindBindingSources(graph, external, derivedTypes), FindSerializableTypes(graph),
            testAssemblies, CollectTestReferences(referenceEdges, testAssemblies));

        var references = ComputeReferenced(referenceEdges, implementations, testAssemblies);

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
        HashSet<string> TestAssemblies,
        Dictionary<string, List<CodeElement>> TestReferences);

    /// <summary>
    ///     What a relationship reaches, once counting every relationship and once counting only those that
    ///     do not start in test code. An element in <see cref="All" /> but not in
    ///     <see cref="FromProduction" /> is used by tests and by nothing else.
    /// </summary>
    private sealed record ReferenceSets(HashSet<string> All, HashSet<string> FromProduction);

    /// <summary>
    ///     The assemblies holding the tests: those containing at least one element with a known
    ///     test-framework attribute.
    ///     <para>
    ///         Deliberately the assembly and not the type. Test helpers - builders, fakes, the graph
    ///         fixture - carry no attribute at all. Deciding per type would make a production member that
    ///         only a helper calls look alive, and would report every helper as used-only-by-tests, which
    ///         floods the result with the whole test project. Tests living <i>in</i> the production
    ///         assembly turn the rule off for that assembly, which is the harmless direction to fail in.
    ///     </para>
    /// </summary>
    private static HashSet<string> FindTestAssemblies(Graph.CodeGraph graph)
    {
        return graph.Nodes.Values
            .Where(element => element.Attributes.Any(TestAttributes.Contains))
            .Select(element => RootOf(element).Id)
            .ToHashSet();
    }

    /// <summary>The assembly an element belongs to - the top of its containment chain.</summary>
    private static CodeElement RootOf(CodeElement element)
    {
        var current = element;
        while (current.Parent is not null)
        {
            current = current.Parent;
        }

        return current;
    }

    private static bool IsTestCode(CodeElement element, HashSet<string> testAssemblies)
    {
        return testAssemblies.Contains(RootOf(element).Id);
    }

    /// <summary>
    ///     Element -&gt; the test elements referencing it, so a used-only-by-tests finding can name what has
    ///     to be deleted with it. Only the relationships starting in test code are walked.
    /// </summary>
    private static Dictionary<string, List<CodeElement>> CollectTestReferences(
        List<(CodeElement Source, CodeElement Target)> referenceEdges, HashSet<string> testAssemblies)
    {
        var testReferences = new Dictionary<string, List<CodeElement>>();

        // Reused across relationships, like in ComputeReferenced.
        var reached = new HashSet<string>();
        var sourceChain = new HashSet<string>();

        foreach (var (source, target) in referenceEdges)
        {
            if (!IsTestCode(source, testAssemblies))
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
    ///     Finds the view models.
    ///     These are the types whose public properties a XAML <c>{Binding}</c> may read - anything implementing
    ///     <c>INotifyPropertyChanged</c>. Bindings are resolved by reflection at runtime and are the one
    ///     XAML construct the parser deliberately does not follow, so such a property must never reach the
    ///     highest confidence.
    ///     <para>
    ///         "Source" in the WPF sense: the object a binding reads from (<c>Binding.Source</c>). The
    ///         binding <i>target</i> is the dependency property on the control, which is not what we look
    ///         for here.
    ///     </para>
    ///     <para>
    ///         The interface shows up through the external contract of the <c>PropertyChanged</c> event.
    ///         A derived view model has no such member of its own (the base class implements it), so the
    ///         property is spread down the <see cref="RelationshipType.Inherits" /> edges - the common
    ///         "MyViewModel : ViewModelBase" shape would be missed otherwise. A base class outside the
    ///         analyzed code is invisible here, so a view model deriving from a framework type that
    ///         implements the interface is not recognized.
    ///     </para>
    /// </summary>
    private static HashSet<string> FindBindingSources(Graph.CodeGraph graph, Dictionary<string, string> external,
        Dictionary<string, List<CodeElement>> derivedTypes)
    {
        var sources = new HashSet<string>();
        var queue = new Queue<string>();

        foreach (var (elementId, contract) in external)
        {
            if (!contract.StartsWith(NotifyPropertyChanged, StringComparison.Ordinal))
            {
                continue;
            }

            var type = ContainingType(graph.TryGetCodeElement(elementId));
            if (type is not null && sources.Add(type.Id))
            {
                queue.Enqueue(type.Id);
            }
        }

        // Add returning false doubles as the visited check, so a diamond cannot enqueue a type twice.
        while (queue.Count > 0)
        {
            if (!derivedTypes.TryGetValue(queue.Dequeue(), out var derived))
            {
                continue;
            }

            foreach (var type in derived.Where(type => sources.Add(type.Id)))
            {
                queue.Enqueue(type.Id);
            }
        }

        return sources;
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
        HashSet<string> testAssemblies)
    {
        return new ReferenceSets(
            Mark(referenceEdges, implementations, _ => true),
            Mark(referenceEdges, implementations, source => !IsTestCode(source, testAssemblies)));
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
        Dictionary<string, List<CodeElement>> implementations, Dictionary<string, List<CodeElement>> contracts,
        Dictionary<string, List<CodeElement>> derivedTypes)
    {
        foreach (var relationship in graph.GetAllRelationships())
        {
            var source = graph.TryGetCodeElement(relationship.SourceId);
            var target = graph.TryGetCodeElement(relationship.TargetId);
            if (source is null || target is null)
            {
                continue;
            }

            if (relationship.Type == RelationshipType.Inherits && source.IsType() && target.IsType())
            {
                Add(derivedTypes, target.Id, source);
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

            // A single accessor is never a finding of its own: the question is whether the property is
            // used, not whether both halves of it are. One unused half is the normal shape of anything a
            // serializer, a binding or a framework drives - a DTO that is written in C# and only read by
            // System.Text.Json has a dead getter on every single property. A property that is dead as a
            // whole is still reported, as the property.
            if (element.ElementType == CodeElementType.PropertyAccessor)
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
    ///         The second one is a finding only outside a test assembly. Inside one it is what is supposed
    ///         to happen - a helper the tests use is doing its job - and reporting it would put the whole
    ///         test project into the result.
    ///     </para>
    ///     <para>
    ///         An external contract does not make an element alive either; it is reported with a note
    ///         instead, so the decision stays visible rather than silently removing rows from the result.
    ///     </para>
    /// </summary>
    private static bool IsReported(CodeElement element, ReferenceSets references, AnalysisContext context)
    {
        if (!IsCandidate(element) || references.FromProduction.Contains(element.Id))
        {
            return false;
        }

        return !references.All.Contains(element.Id) || !IsTestCode(element, context.TestAssemblies);
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

            foreach (var attribute in member.Attributes)
            {
                if (TestAttributes.Contains(attribute))
                {
                    hints |= DeadCodeHint.TestCode;
                }
                else
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
    ///     Whether a XAML <c>{Binding}</c> could read this element without us seeing it. That takes two
    ///     things: a <b>public</b> property - the binding engine resolves by public reflection, so private,
    ///     internal and protected members are out of its reach - on a type that raises change
    ///     notifications.
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
    ///     Whether the element is a public property of one of the given types. Accessors are never findings
    ///     of their own (see <see cref="Report" />), so the reported element is the property itself.
    /// </summary>
    private static bool IsPublicPropertyOf(CodeElement element, HashSet<string> types)
    {
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
    ///     elements are out of scope - we see neither their callers nor their bodies.
    /// </summary>
    private static bool IsCandidate(CodeElement element)
    {
        return !element.IsExternal &&
               element.ElementType is not (CodeElementType.Assembly or CodeElementType.Namespace);
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

        // A static constructor is run by the runtime before the first use of the type. Nothing in the
        // code ever references it, so without this it looks like a particularly trustworthy finding -
        // it is usually private, which would otherwise put it in the highest confidence band.
        if (element is { ElementType: CodeElementType.Method, Name: ".cctor" })
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
