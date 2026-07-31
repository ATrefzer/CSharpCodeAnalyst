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
///         The analysis cascades. Round 1 finds what nothing references at all. Every following round
///         ignores the outgoing references of what was already found, so code that is only kept alive by
///         dead code dies with it - the chain "nobody calls Report, Report calls Formatter, nothing else
///         calls Formatter" collapses completely. <see cref="DeadCodeFinding.Level" /> says which round a
///         finding comes from.
///     </para>
///     <para>
///         Only findings without a note propagate (see <see cref="PropagatesDeath" />). This is not a
///         detail: the class holding <c>Main</c> is a round-1 finding, and letting it propagate would
///         declare the entire application dead in the following rounds. The same holds for test fixtures
///         and for members the framework calls. They are still reported - they simply do not take anything
///         with them.
///     </para>
///     <para>
///         Every finding carries a <see cref="DeadCodeFinding.Confidence" />, and
///         <see cref="AccessLevel" /> is what makes the top level reachable: an element confined to its
///         type or assembly cannot be referenced from code we did not analyze, so "nothing references it"
///         and "nothing can reference it" coincide. A producer that supplies no visibility never reaches
///         that level - which is the honest answer, not a penalty.
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
    ///     The notes that say "the caller is somewhere we cannot see". A finding carrying one of them is
    ///     reported but never used as evidence that something else is dead.
    /// </summary>
    private const DeadCodeHint CallerOutsideTheGraph =
        DeadCodeHint.EntryPoint | DeadCodeHint.TestCode | DeadCodeHint.Attributed |
        DeadCodeHint.ImplementsExternalContract;

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

        // The structure never changes between rounds - only which sources still count does.
        var referenceEdges = new List<(CodeElement Source, CodeElement Target)>();
        CollectEdges(graph, referenceEdges, external, implementations, contracts);

        // Everything found dead so far, including the subtrees of the reported elements.
        var found = new HashSet<string>();

        // The subset whose outgoing references are ignored from the next round on.
        var silenced = new HashSet<string>();

        var findings = new List<DeadCodeFinding>();

        for (var level = 1;; level++)
        {
            var referenced = ComputeReferenced(referenceEdges, silenced, implementations);
            var round = Report(graph, referenced, found, external, implementations, contracts, level);
            if (round.Count == 0)
            {
                break;
            }

            findings.AddRange(round);
            foreach (var finding in round)
            {
                // The note about an external contract sits on the member, but the decision to propagate
                // has to look at the whole subtree: a dead class holding an ICommand.Execute is reported
                // without that note (it is the class that is dead), yet its calls may well still run.
                var propagates = PropagatesDeath(finding) &&
                                 !finding.Element.GetSubtreeIncludingSelf().Any(e => external.ContainsKey(e.Id));
                foreach (var element in finding.Element.GetSubtreeIncludingSelf())
                {
                    found.Add(element.Id);
                    if (propagates)
                    {
                        silenced.Add(element.Id);
                    }
                }
            }
        }

        return findings.OrderBy(f => f.Element.FullName, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    ///     Whether a finding may be used as evidence that something else is dead. Anything whose caller
    ///     sits outside the graph must not: the class holding <c>Main</c> is reported, but treating its
    ///     calls as gone would take the whole application down with it in the next round.
    /// </summary>
    private static bool PropagatesDeath(DeadCodeFinding finding)
    {
        return (finding.Hints & CallerOutsideTheGraph) == DeadCodeHint.None;
    }

    /// <summary>
    ///     Recomputes who is referenced, ignoring everything that comes out of already dead code. The set
    ///     only ever shrinks from round to round, so nothing that was reported can come back to life.
    /// </summary>
    private static HashSet<string> ComputeReferenced(
        List<(CodeElement Source, CodeElement Target)> referenceEdges, HashSet<string> silenced,
        Dictionary<string, List<CodeElement>> implementations)
    {
        var referenced = new HashSet<string>();

        // Reused across relationships to keep the walk allocation free.
        var sourceChain = new HashSet<string>();

        foreach (var (source, target) in referenceEdges)
        {
            if (!silenced.Contains(source.Id))
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
    ///     The findings of a single round: everything unreferenced that was not already found earlier.
    /// </summary>
    private static List<DeadCodeFinding> Report(Graph.CodeGraph graph, HashSet<string> referenced,
        HashSet<string> found, Dictionary<string, string> external,
        Dictionary<string, List<CodeElement>> implementations,
        Dictionary<string, List<CodeElement>> contracts, int level)
    {
        var findings = new List<DeadCodeFinding>();

        foreach (var element in graph.Nodes.Values)
        {
            // An external contract does not make the element alive - it is reported with a note instead,
            // so the decision stays visible rather than silently removing rows from the result.
            if (!IsCandidate(element) || referenced.Contains(element.Id) || found.Contains(element.Id))
            {
                continue;
            }

            // Roll-up: a dead element inside a dead element is reported as part of it. Namespaces and
            // assemblies are no candidates, so an element directly below them is always the topmost one.
            var parent = element.Parent;
            if (parent is not null && IsCandidate(parent) && !referenced.Contains(parent.Id))
            {
                continue;
            }

            findings.Add(CreateFinding(element, external, implementations, contracts, level));
        }

        return findings;
    }

    private static DeadCodeFinding CreateFinding(CodeElement element, Dictionary<string, string> external,
        Dictionary<string, List<CodeElement>> implementations, Dictionary<string, List<CodeElement>> contracts,
        int level)
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
        if (contracts.TryGetValue(element.Id, out var implemented))
        {
            hints |= DeadCodeHint.ImplementsDeadContract;
            related.AddRange(implemented);
        }

        if (implementations.TryGetValue(element.Id, out var implementors))
        {
            hints |= DeadCodeHint.ContractNeverCalled;
            related.AddRange(implementors);
        }

        // Element level only. A dead class whose members implement IDisposable is still dead - saying
        // "might be used" about the class would be wrong, the note belongs to the member.
        external.TryGetValue(element.Id, out var externalContract);
        if (externalContract is not null)
        {
            hints |= DeadCodeHint.ImplementsExternalContract;
        }

        return new DeadCodeFinding(element)
        {
            Level = level,
            Confidence = RateConfidence(element, hints, level),
            Hints = hints,
            Attributes = attributes.ToList(),
            RelatedMembers = related,
            ExternalContract = externalContract
        };
    }

    /// <summary>
    ///     Three rules, in order. A note about a caller outside the graph beats everything - we already
    ///     know the finding may be wrong. Otherwise visibility decides, but only for a direct finding:
    ///     what the cascade produced is never better than the rounds it rests on.
    /// </summary>
    private static DeadCodeConfidence RateConfidence(CodeElement element, DeadCodeHint hints, int level)
    {
        if ((hints & CallerOutsideTheGraph) != DeadCodeHint.None)
        {
            return DeadCodeConfidence.Low;
        }

        if (level == 1 && IsConfinedToAnalyzedCode(element))
        {
            return DeadCodeConfidence.High;
        }

        return DeadCodeConfidence.Medium;
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
