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
///         Limitations, by construction: references the parser cannot see (XAML, reflection, dependency
///         injection, serialization) look like dead code - see <see cref="DeadCodeHint" />. Accessibility
///         is not part of the graph, so the public API of a library cannot be treated as used. And because
///         this is the direct variant, an element stays alive when a dead element references it; only a
///         cascading analysis would collapse whole dead clusters.
///     </para>
/// </summary>
public static class DeadCodeAnalysis
{
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

        // Alive because something references it (directly or through a contract).
        var referenced = new HashSet<string>();

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

        CollectEdges(graph, referenced, external, implementations, contracts);
        PropagateContractUsage(referenced, implementations);

        return Report(graph, referenced, external, implementations, contracts);
    }

    private static void CollectEdges(Graph.CodeGraph graph, HashSet<string> referenced,
        Dictionary<string, string> external,
        Dictionary<string, List<CodeElement>> implementations, Dictionary<string, List<CodeElement>> contracts)
    {
        // Reused across relationships to keep the walk allocation free.
        var sourceChain = new HashSet<string>();

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

            MarkReferenced(source, target, referenced, sourceChain);
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

    private static List<DeadCodeFinding> Report(Graph.CodeGraph graph, HashSet<string> referenced,
        Dictionary<string, string> external, Dictionary<string, List<CodeElement>> implementations,
        Dictionary<string, List<CodeElement>> contracts)
    {
        var findings = new List<DeadCodeFinding>();

        foreach (var element in graph.Nodes.Values)
        {
            // An external contract does not make the element alive - it is reported with a note instead,
            // so the decision stays visible rather than silently removing rows from the result.
            if (!IsCandidate(element) || referenced.Contains(element.Id))
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

            findings.Add(CreateFinding(element, external, implementations, contracts));
        }

        return findings.OrderBy(f => f.Element.FullName, StringComparer.Ordinal).ToList();
    }

    private static DeadCodeFinding CreateFinding(CodeElement element, Dictionary<string, string> external,
        Dictionary<string, List<CodeElement>> implementations, Dictionary<string, List<CodeElement>> contracts)
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
            Hints = hints,
            Attributes = attributes.ToList(),
            RelatedMembers = related,
            ExternalContract = externalContract
        };
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
