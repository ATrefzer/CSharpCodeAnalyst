using CSharpCodeAnalyst.CodeGraph.Algorithms.Cycles;
using CSharpCodeAnalyst.CodeGraph.Contracts;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.CodeGraph.Exploration;

public class CodeGraphExplorer : ICodeGraphExplorer
{
    private Graph.CodeGraph? _codeGraph;

    public void LoadCodeGraph(Graph.CodeGraph graph)
    {
        _codeGraph = graph;
    }

    public List<CodeElement> GetElements(List<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (_codeGraph is null)
        {
            return [];
        }

        List<CodeElement> elements = [];
        foreach (var id in ids)
        {
            if (_codeGraph.Nodes.TryGetValue(id, out var element))
            {
                elements.Add(element);
            }
        }

        return elements;
    }

    /// <summary>
    ///     Adds the containers for all low level elements like fields or methods
    ///     to give more context.
    ///     The method fills also any missing intermediate containers.
    /// </summary>
    public SearchResult FindMissingTypesForLonelyTypeMembers(HashSet<string> knownIds)
    {
        if (_codeGraph is null)
        {
            return new SearchResult([], []);
        }

        var parentIds = new HashSet<string>();

        foreach (var id in knownIds)
        {
            if (!_codeGraph.Nodes.TryGetValue(id, out var current))
            {
                continue; // Skip invalid id
            }

            // Walk up the parent chain of each non-container element and collect the missing containers.
            while (current.Parent is not null &&
                   CodeElementClassifier.GetContainerLevel(current.ElementType) == 0)
            {
                if (!knownIds.Contains(current.Parent.Id))
                {
                    parentIds.Add(current.Parent.Id);
                }

                current = current.Parent;
            }
        }

        // Fill also gaps.
        var gapFillingIds = FillGapsInHierarchy(knownIds);
        var elementIds = gapFillingIds.Union(parentIds);

        var elements = elementIds.Select(p => _codeGraph.Nodes[p]).ToHashSet();
        return new SearchResult(elements, []);
    }

    /// <summary>
    ///     All relationships crossing the element's boundary outwards: the source is the element
    ///     or one of its descendants, the target lies outside. Relationships between two
    ///     descendants are internal and not part of the result.
    ///     The result contains the start element, the elements taking part in a found relationship
    ///     and the containers connecting those to the start element. The reached targets come
    ///     without their gaps filled. If the caller needs those, it runs
    ///     <see cref="FindGapsInHierarchy" /> or <see cref="FindMissingTypesForLonelyTypeMembers" />
    ///     on the result.
    /// </summary>
    public SearchResult FindOutgoingRelationshipsDeep(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (_codeGraph is null || !_codeGraph.Nodes.TryGetValue(id, out var element))
        {
            return new SearchResult([], []);
        }

        var subtree = element.GetChildrenIncludingSelf();
        var relationships = _codeGraph.GetAllRelationships()
            .Where(r => subtree.Contains(r.SourceId) && !subtree.Contains(r.TargetId)).ToList();

        return CollectDeepResult(element, relationships);
    }

    /// <summary>
    ///     All relationships crossing the element's boundary inwards: the target is the element
    ///     or one of its descendants, the source lies outside. Relationships between two
    ///     descendants are internal and not part of the result.
    ///     The result contains the start element, the elements taking part in a found relationship
    ///     and the containers connecting those to the start element. The found sources come
    ///     without their gaps filled. If the caller needs those, it runs
    ///     <see cref="FindGapsInHierarchy" /> or <see cref="FindMissingTypesForLonelyTypeMembers" />
    ///     on the result.
    /// </summary>
    public SearchResult FindIncomingRelationshipsDeep(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (_codeGraph is null || !_codeGraph.Nodes.TryGetValue(id, out var element))
        {
            return new SearchResult([], []);
        }

        var subtree = element.GetChildrenIncludingSelf();
        var relationships = _codeGraph.GetAllRelationships()
            .Where(r => subtree.Contains(r.TargetId) && !subtree.Contains(r.SourceId)).ToList();

        return CollectDeepResult(element, relationships);
    }

    public SearchResult FindParents(List<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (_codeGraph is null)
        {
            return new SearchResult([], []);
        }

        var parents = new HashSet<CodeElement>();
        foreach (var id in ids)
        {
            if (_codeGraph.Nodes.TryGetValue(id, out var element))
            {
                if (element.Parent is not null)
                {
                    parents.Add(element.Parent);
                }
            }
        }

        return new SearchResult(parents, []);
    }

    /// <summary>
    ///     Returns all relationships that link the given nodes (ids).
    /// </summary>
    public IEnumerable<Relationship> FindAllRelationships(HashSet<string> ids)
    {
        if (_codeGraph is null)
        {
            return [];
        }

        return GetRelationships(d => ids.Contains(d.SourceId) && ids.Contains(d.TargetId));
    }

    /// <summary>
    ///     All relationships crossing the root element's boundary inwards or outwards.
    ///     The result however is limited to be contained in the given roots
    ///     The result contains the start element, the elements taking part in a found relationship
    ///     and the gap filling elements.
    /// 
    ///     FindIncomingRelationshipsDeep / FindOutgoingRelationshipsDeep have an open result. Here,
    ///     all new nodes are descendants of the root elements.
    /// </summary>
    public SearchResult FindAllRelationshipsDeep(HashSet<string> rootIds)
    {
        if (_codeGraph is null)
        {
            return new SearchResult([], []);
        }

        // Expanded set per id
        // To which selected root does an element belong. This may be more than one since
        // the roots may be nested (e.g. class and namespace are selected. Both contain same method.)
        var rootsByElement = new Dictionary<string, HashSet<string>>();
        foreach (var rootId in rootIds.Where(_codeGraph.Nodes.ContainsKey))
        {
            foreach (var id in _codeGraph.Nodes[rootId].GetChildrenIncludingSelf())
            {
                if (!rootsByElement.TryGetValue(id, out var roots))
                {
                    roots = [];
                    rootsByElement[id] = roots;
                }

                roots.Add(rootId);
            }
        }

        // Are there a source root s and a target root t that are different?” That is exactly the definition of a crossing edge.
        bool CrossesRoots(Relationship r)
        {
            return rootsByElement.TryGetValue(r.SourceId, out var sourceRoots) &&
                   rootsByElement.TryGetValue(r.TargetId, out var targetRoots) &&
                   sourceRoots.Any(s => targetRoots.Any(t => s != t));
        }

        var relationships = _codeGraph.GetAllRelationships().Where(CrossesRoots).ToHashSet();

        // We collected all edges.
        var relationshipElementIds = relationships
            .SelectMany(r => new[] { r.SourceId, r.TargetId })
            .ToHashSet();

        // Found elements can be deep inside the given roots (e.g. two methods buried in two
        // selected assemblies). Fill in the namespaces/classes connecting them to a root that is
        // already known, so they don't end up added without their hierarchy.
        var gapIds = FillGapsInHierarchy(rootIds.Union(relationshipElementIds).ToHashSet());

        var elements = relationshipElementIds
            .Union(gapIds)
            .Union(rootIds) // to be self-contained
            .Where(_codeGraph.Nodes.ContainsKey)
            .Select(id => _codeGraph.Nodes[id])
            .ToHashSet();

        return new SearchResult(elements, relationships);
    }

    public SearchResult FindIncomingCalls(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (_codeGraph is null || !_codeGraph.Nodes.TryGetValue(id, out var method))
        {
            return new SearchResult([], []);
        }

        var calls = GetRelationships(d => d.Type == RelationshipType.Calls && d.TargetId == method.Id);
        var callers = calls.Select(d => _codeGraph.Nodes[d.SourceId]);

        return new SearchResult(callers, calls);
    }


    /// <summary>
    ///     Follows the direct "Calls" edges to the callers, transitively. Unlike
    ///     <see cref="FollowIncomingCallsHeuristically" /> it does not follow abstractions
    ///     (interface or base declarations) or event handling, so callers that reach the method
    ///     through virtual dispatch or events are not found.
    /// </summary>
    public SearchResult FindIncomingCallsRecursive(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (_codeGraph is null || !_codeGraph.Nodes.TryGetValue(id, out var method))
        {
            return new SearchResult([], []);
        }

        var processingQueue = new Queue<CodeElement>();
        processingQueue.Enqueue(method);

        var foundCalls = new HashSet<Relationship>();
        var foundMethods = new HashSet<CodeElement>();

        var callsByTarget = GetRelationships(d => d.Type == RelationshipType.Calls)
            .ToLookup(call => call.TargetId);

        var processed = new HashSet<string>();
        while (processingQueue.Count > 0)
        {
            var element = processingQueue.Dequeue();
            if (!processed.Add(element.Id))
            {
                continue;
            }

            var calls = callsByTarget[element.Id].ToArray();
            foundCalls.UnionWith(calls);

            var methods = calls.Select(d => _codeGraph.Nodes[d.SourceId]).ToArray();
            foundMethods.UnionWith(methods);

            foreach (var methodToExplore in methods)
            {
                processingQueue.Enqueue(methodToExplore);
            }
        }

        return new SearchResult(foundMethods, foundCalls);
    }

    /// <summary>
    ///     This gives a heuristic only.
    ///     The graph model does not contain the information for analyzing dynamic behavior.
    ///     The result is the union over all paths: a caller is included if there is at least
    ///     one path under which it can reach the start method. The result does not depend on
    ///     the processing order.
    /// </summary>
    public SearchResult FollowIncomingCallsHeuristically(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (_codeGraph is null || !_codeGraph.Nodes.TryGetValue(id, out var start))
        {
            return new SearchResult([], []);
        }

        var graph = _codeGraph;

        // One scan over the graph, then O(1) lookups: the loop below queries these sets once
        // per visited element, and a linear search each time made the whole walk quadratic.
        var allRelationships = graph.GetAllRelationships().ToList();
        var implementsAndOverrides = allRelationships
            .Where(d => d.Type is RelationshipType.Implements or RelationshipType.Overrides).ToList();
        var abstractionsBySource = implementsAndOverrides.ToLookup(d => d.SourceId);
        var specializationsByTarget = implementsAndOverrides.ToLookup(d => d.TargetId);
        var callsByTarget = allRelationships
            .Where(d => d.Type == RelationshipType.Calls).ToLookup(d => d.TargetId);
        var handledEventsBySource = allRelationships
            .Where(d => d.Type == RelationshipType.Handles).ToLookup(d => d.SourceId);
        var invokersByTarget = allRelationships
            .Where(d => d.Type == RelationshipType.Invokes).ToLookup(d => d.TargetId);

        // The forbidden set depends only on the method's container, and computing it walks the
        // whole inheritance structure - so it is cached per container for this search. The cached
        // set is shared read-only between contexts; CreateContextForCaller clones before it unions.
        var inheritanceByTarget = FindInheritsAndImplementsRelationships().ToLookup(r => r.TargetId);
        var forbiddenByContainer = new Dictionary<string, HashSet<CodeElement>>();

        var processingQueue = new Queue<(CodeElement Element, Context Context)>();
        processingQueue.Enqueue((start, CreateContextForMethod(start)));

        var foundRelationships = new HashSet<Relationship>();

        // The start element is added for convenience, so the result is self-contained.
        var foundElements = new HashSet<CodeElement> { start };

        // An element can be reached over several paths with different contexts. The context
        // (the forbidden call sources) is path dependent: implicit, "this" and "base" calls
        // and the abstraction walk pass the current context on, while instance, static and
        // extension calls and event invocations reset it. The desired semantics is
        // "is there ANY path?", so the element must contribute the callers allowed under
        // each arriving context.
        //
        // Therefore, we remember per element all forbidden sets it has been processed with.
        // A new context is skipped only if a previous pass was at least as permissive, i.e.
        // its forbidden set was a subset of the new one - that pass has already found
        // everything the new context could find. Otherwise, the element is processed again;
        // the found sets de-duplicate any overlap.
        //
        // Termination: forbidden sets are unions of finitely many hierarchy restrictions, so
        // per element only finitely many distinct sets exist, and each reprocessing registers
        // a set that no earlier registered set was a subset of.
        var processed = new Dictionary<string, List<HashSet<CodeElement>>>();

        while (processingQueue.Count > 0)
        {
            var (element, context) = processingQueue.Dequeue();
            if (!TryMarkProcessed(element, context))
            {
                continue;
            }

            if (element.ElementType == CodeElementType.Event)
            {
                // The event may be declared on a base type or an interface.
                // The specialized events are the ones the publishers actually raise.
                var specializedEvents = Collect(specializationsByTarget[element.Id], d => d.SourceId);
                AddToProcessingQueue(specializedEvents, context);

                // The publisher side is unrelated to
                // the subscriber's hierarchy, so continue as if the search started at the invoker.
                // Raising the event does not use virtual dispatch on "this": the handlers were
                // bound at registration time and are reached through the delegate's invocation list.
                var invokers = Collect(invokersByTarget[element.Id], d => d.SourceId);
                foreach (var invoker in invokers)
                {
                    AddToProcessingQueue([invoker], CreateContextForMethod(invoker));
                }
            }

            // Properties take part in call chains like methods do:
            // accessor bodies are call sources and property accesses are modeled as calls.
            // When property accessors are split, the get_/set_ elements carry those calls instead.
            if (element.ElementType is CodeElementType.Method or CodeElementType.Property or CodeElementType.PropertyAccessor)
            {
                // Follow the callers whose calls can dispatch to this element under the
                // current restrictions. Each caller gets the context for its kind of call.
                var calls = callsByTarget[element.Id].Where(context.IsCallAllowed);
                foreach (var call in calls)
                {
                    foundRelationships.Add(call);
                    var caller = graph.Nodes[call.SourceId];
                    foundElements.Add(caller);
                    AddToProcessingQueue([caller], CreateContextForCaller(call, caller, context));
                }

                // The abstractions (interface or base declarations) may be called instead of
                // the concrete element. Follow them with the current restrictions.
                var abstractions = Collect(abstractionsBySource[element.Id], d => d.TargetId);
                AddToProcessingQueue(abstractions, context);

                // If this element handles an event, the chain continues at the event.
                var handledEvents = Collect(handledEventsBySource[element.Id], d => d.TargetId);
                AddToProcessingQueue(handledEvents, context);
            }
        }

        return new SearchResult(foundElements, foundRelationships);


        // Adds the relationships and their adjacent elements (selected by id) to the result.
        HashSet<CodeElement> Collect(IEnumerable<Relationship> relationships, Func<Relationship, string> selectId)
        {
            var collected = relationships.ToArray();
            foundRelationships.UnionWith(collected);

            var elements = collected.Select(d => graph.Nodes[selectId(d)]).ToHashSet();
            foundElements.UnionWith(elements);
            return elements;
        }

        // The call attributes describe how the current element was called. They decide
        // which restrictions apply when the search continues at the caller.
        Context CreateContextForCaller(Relationship call, CodeElement caller, Context context)
        {
            if (call.HasAttribute(RelationshipAttribute.IsBaseCall))
            {
                // A base call pins the runtime type to the caller's subtree.
                // The caller's hierarchy restrictions apply in addition to the current ones.
                var restricted = context.Clone();
                restricted.ForbiddenCallSourcesInHierarchy.UnionWith(GetForbiddenCallSources(caller));
                return restricted;
            }

            if (!call.DispatchesOnCurrentInstance())
            {
                // An instance, static or extension method call breaks the dispatch chain of the
                // current "this" instance. Continue as if the search started at the caller,
                // restricted to its hierarchy.
                return CreateContextForMethod(caller);
            }

            // Implicit and "this" calls stay within the current dispatch chain.
            return context;
        }

        // Creates the context for exploring the callers of the given method, as if the
        // search started there.
        Context CreateContextForMethod(CodeElement method)
        {
            return new Context(graph)
            {
                ForbiddenCallSourcesInHierarchy = GetForbiddenCallSources(method)
            };
        }

        HashSet<CodeElement> GetForbiddenCallSources(CodeElement method)
        {
            var container = GetMethodContainer(method);
            if (container is null)
            {
                return [];
            }

            if (!forbiddenByContainer.TryGetValue(container.Id, out var forbidden))
            {
                forbidden = GetForbiddenCallSourcesInHierarchy(container, inheritanceByTarget);
                forbiddenByContainer[container.Id] = forbidden;
            }

            return forbidden;
        }

        void AddToProcessingQueue(IEnumerable<CodeElement> elementsToExplore, Context context)
        {
            foreach (var elementToExplore in elementsToExplore)
            {
                processingQueue.Enqueue((elementToExplore, context));
            }
        }

        // Returns true if the element has to be processed with the given context.
        // See the comment at the declaration of "processed" for the semantics.
        bool TryMarkProcessed(CodeElement element, Context context)
        {
            var forbidden = context.ForbiddenCallSourcesInHierarchy;

            if (!processed.TryGetValue(element.Id, out var seenForbiddenSets))
            {
                seenForbiddenSets = [];
                processed[element.Id] = seenForbiddenSets;
            }

            if (seenForbiddenSets.Any(seen => seen.IsSubsetOf(forbidden)))
            {
                // A previous pass was at least as permissive. Nothing new to find.
                return false;
            }

            // Earlier, more restrictive sets are covered by the new pass and obsolete now.
            seenForbiddenSets.RemoveAll(seen => forbidden.IsSubsetOf(seen));

            // Keep a snapshot so later context mutations cannot corrupt the bookkeeping.
            seenForbiddenSets.Add(forbidden.ToHashSet());
            return true;
        }
    }

    /// <summary>
    ///     subclass -- inherits--> baseclass
    /// </summary>
    public SearchResult FindFullInheritanceTree(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (_codeGraph is null || !_codeGraph.Nodes.TryGetValue(id, out var type))
        {
            return new SearchResult([], []);
        }

        var inheritanceByTarget = FindInheritsAndImplementsRelationships().ToLookup(r => r.TargetId);
        var (types, relationships) = CollectFullInheritanceTree(type, inheritanceByTarget);

        return new SearchResult(types, relationships);
    }

    /// <summary>
    ///     x (source) -- derives from/overrides/implements --> y (target, search input)
    /// </summary>
    public SearchResult FindSpecializations(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (_codeGraph is null || !_codeGraph.Nodes.TryGetValue(id, out var element))
        {
            return new SearchResult([], []);
        }

        var relationships = _codeGraph.GetAllRelationships()
            .Where(d =>
                d.Type is RelationshipType.Overrides or RelationshipType.Inherits or RelationshipType.Implements &&
                d.TargetId == element.Id).ToList();
        var specializations = relationships.Select(m => _codeGraph.Nodes[m.SourceId]).ToList();
        return new SearchResult(specializations, relationships);
    }

    /// <summary>
    ///     x (source, search input) -- derives from/overrides/implements --> y (target)
    /// </summary>
    public SearchResult FindAbstractions(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (_codeGraph is null || !_codeGraph.Nodes.TryGetValue(id, out var element))
        {
            return new SearchResult([], []);
        }

        var relationships = element.Relationships
            .Where(d =>
                d.Type is RelationshipType.Overrides or RelationshipType.Inherits or RelationshipType.Implements &&
                d.SourceId == element.Id).ToList();
        var abstractions = relationships.Select(m => _codeGraph.Nodes[m.TargetId]).ToList();
        return new SearchResult(abstractions, relationships);
    }


    public SearchResult FindOutgoingCalls(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (_codeGraph is null || !_codeGraph.Nodes.TryGetValue(id, out var method))
        {
            return new SearchResult([], []);
        }

        var calls = method.Relationships
            .Where(d => d.Type == RelationshipType.Calls).ToList();
        var callees = calls.Select(m => _codeGraph.Nodes[m.TargetId]).ToList();
        return new SearchResult(callees, calls);
    }

    public SearchResult FindOutgoingRelationships(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (_codeGraph is null || !_codeGraph.Nodes.TryGetValue(id, out var element))
        {
            return new SearchResult([], []);
        }

        var relationships = element.Relationships;
        var targets = relationships.Select(m => _codeGraph.Nodes[m.TargetId]).ToList();
        return new SearchResult(targets, relationships);
    }

    public SearchResult FindIncomingRelationships(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (_codeGraph is null || !_codeGraph.Nodes.TryGetValue(id, out var element))
        {
            return new SearchResult([], []);
        }

        var relationships = _codeGraph.GetAllRelationships()
            .Where(d => d.TargetId == element.Id).ToList();

        var elements = relationships.Select(d => _codeGraph.Nodes[d.SourceId]);

        return new SearchResult(elements, relationships);
    }

    public IReadOnlyList<string> GetWithPropertyAccessors(string id)
    {
        if (_codeGraph is null || !_codeGraph.Nodes.TryGetValue(id, out var element)
                               || element.ElementType != CodeElementType.Property)
        {
            return [id];
        }

        var ids = new List<string> { id };
        ids.AddRange(element.Children
            .Where(c => c.ElementType == CodeElementType.PropertyAccessor)
            .Select(c => c.Id));

        return ids;
    }

    public SearchResult ExploreWithAccessors(string id, Func<string, SearchResult> explore)
    {
        var ids = GetWithPropertyAccessors(id);

        var elements = new HashSet<CodeElement>();
        var relationships = new HashSet<Relationship>();

        // Always include the property and all its accessors as nodes so that
        // any relationship touching them has a valid endpoint in the result.
        foreach (var expandedId in ids)
        {
            if (_codeGraph?.Nodes.TryGetValue(expandedId, out var node) == true)
            {
                elements.Add(node);
            }
        }

        foreach (var expandedId in ids)
        {
            var result = explore(expandedId);
            elements.UnionWith(result.Elements);
            relationships.UnionWith(result.Relationships);
        }

        return new SearchResult(elements, relationships);
    }

    /// <summary>
    ///     Builds the result of the deep searches: the endpoints of the found relationships, the
    ///     code elements filling the gaps to the start element, and the start element itself so the
    ///     result is self-contained even when nothing was found.
    /// </summary>
    private SearchResult CollectDeepResult(CodeElement startElement, List<Relationship> relationships)
    {
        var relationshipElementIds = relationships
            .SelectMany(r => new[] { r.SourceId, r.TargetId })
            .ToHashSet();

        // The involved elements can sit deep inside the start element; fill in the containers
        // connecting them to it, so they are not added without their hierarchy.
        var gapIds = FillGapsInHierarchy(relationshipElementIds.Union([startElement.Id]).ToHashSet());

        var elements = relationshipElementIds
            .Union(gapIds)
            .Union([startElement.Id])
            .Where(_codeGraph!.Nodes.ContainsKey)
            .Select(i => _codeGraph!.Nodes[i])
            .ToHashSet();

        return new SearchResult(elements, relationships);
    }

    /// <summary>
    ///     The traversal behind <see cref="FindFullInheritanceTree" />. Callers that need the tree
    ///     of many types (the forbidden-set calculation) pass the inheritance lookup in, so the
    ///     graph is scanned once instead of once per type.
    ///     Interfaces are included.
    /// </summary>
    private (HashSet<CodeElement> Types, HashSet<Relationship> Relationships) CollectFullInheritanceTree(CodeElement type, ILookup<string, Relationship> inheritanceByTarget)
    {
        // Include the interfaces.
        var baseTypes = GetBaseTypesRecursive(type, true);
        var derivedTypes = GetDerivedTypesRecursive(type, inheritanceByTarget, true);

        var types = baseTypes.Union(derivedTypes).Union([type]).ToHashSet();
        var typeIds = types.Select(t => t.Id).ToHashSet();

        // Get the relationships
        var relationships = _codeGraph!.GetAllRelationships()
            .Where(r => r.Type is RelationshipType.Inherits or RelationshipType.Implements &&
                        typeIds.Contains(r.SourceId) &&
                        typeIds.Contains(r.TargetId)).ToHashSet();

        return (types, relationships);
    }

    /// <summary>
    ///     The method finds and returns the missing code elements in the hierarchy.
    /// </summary>
    public SearchResult FindGapsInHierarchy(HashSet<string> knownIds)
    {
        if (_codeGraph is null)
        {
            return new SearchResult([], []);
        }

        var newElements = FillGapsInHierarchy(knownIds);

        var elements = newElements.Select(p => _codeGraph.Nodes[p]).ToHashSet();
        return new SearchResult(elements, []);
    }


    private HashSet<string> FillGapsInHierarchy(HashSet<string> knownIds)
    {
        var added = new HashSet<string>();

        if (_codeGraph is null)
        {
            return added;
        }

        foreach (var id in knownIds)
        {
            if (!_codeGraph.Nodes.TryGetValue(id, out var current))
            {
                continue; // Skip invalid Id
            }

            var fromRootToCurrent = current.GetPathToRoot(false).ToList();
            var addFromHere = false;
            foreach (var parent in fromRootToCurrent)
            {
                if (addFromHere)
                {
                    added.Add(parent.Id);
                    continue;
                }

                if (knownIds.Contains(parent.Id))
                {
                    // Found a parent, all elements from here down to current need to be present.
                    addFromHere = true;
                }
            }
        }

        return added;
    }


    /// <summary>
    ///     Returns the classes that cannot be the source of an implicit, "this" or "base" call
    ///     reaching a method of the given container: the side branches of its class hierarchy.
    /// </summary>
    private HashSet<CodeElement> GetForbiddenCallSourcesInHierarchy(
        CodeElement container, ILookup<string, Relationship> inheritanceByTarget)
    {
        if (_codeGraph is null)
        {
            return [];
        }

        var baseClasses = GetBaseTypesRecursive(container);

        // Non instance calls may originate from these classes.
        var allowedHierarchy = baseClasses
            .Union(GetDerivedTypesRecursive(container, inheritanceByTarget))
            .Union([container])
            .ToHashSet();

        // Side hierarchies originating from a shared base class.
        var expandedBaseClasses = new HashSet<CodeElement>();
        foreach (var baseClass in baseClasses)
        {
            expandedBaseClasses.UnionWith(CollectFullInheritanceTree(baseClass, inheritanceByTarget).Types);
        }

        // The inheritance tree also contains the interfaces of the base classes.
        // They must not be forbidden: an implicit call from a default interface method
        // can dispatch to any implementing class, including the followed hierarchy branch.
        var forbiddenHierarchy = expandedBaseClasses
            .Where(e => e.ElementType is CodeElementType.Class or CodeElementType.Struct or CodeElementType.Record)
            .Except(allowedHierarchy)
            .ToHashSet();
        return forbiddenHierarchy;
    }

    private HashSet<CodeElement> GetDerivedTypesRecursive(CodeElement? element, ILookup<string, Relationship> inheritanceByTarget, bool includeInterfaces = false)
    {
        if (_codeGraph is null || element is null)
        {
            return [];
        }

        var derivedTypes = new HashSet<CodeElement>();
        var queue = new Queue<CodeElement>();
        queue.Enqueue(element);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();


            var specializations = inheritanceByTarget[current.Id]
                .Where(r => r.Type == RelationshipType.Inherits || includeInterfaces && r.Type == RelationshipType.Implements);

            foreach (var relation in specializations)
            {
                var derivedType = _codeGraph.Nodes[relation.SourceId];

                // Only enqueue an element we have not visited yet.
                if (derivedTypes.Add(derivedType))
                {
                    queue.Enqueue(derivedType);
                }
            }
        }

        return derivedTypes;
    }

    /// <summary>
    ///     The given element is not included in the result.
    /// </summary>
    private HashSet<CodeElement> GetBaseTypesRecursive(CodeElement? element, bool includeInterfaces = false)
    {
        if (_codeGraph is null || element is null)
        {
            return [];
        }

        var baseTypes = new HashSet<CodeElement>();
        var queue = new Queue<CodeElement>();
        queue.Enqueue(element);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            var targets = current.Relationships
                .Where(d => d.SourceId == current.Id &&
                            (d.Type == RelationshipType.Inherits || includeInterfaces && d.Type == RelationshipType.Implements))
                .Select(m => _codeGraph.Nodes[m.TargetId]);

            // The parser models interface-to-interface inheritance as Implements; Inherits comes
            // only from a type's BaseType, and C# allows a single base class.

            foreach (var baseType in targets)
            {
                if (baseTypes.Add(baseType))
                {
                    queue.Enqueue(baseType);
                }
            }
        }

        return baseTypes;
    }

    public static CodeElement? GetMethodContainer(CodeElement method)
    {
        var element = method;
        while (element.ElementType is not (CodeElementType.Class or CodeElementType.Struct or CodeElementType.Interface or CodeElementType.Record))
        {
            if (element.Parent is null)
            {
                return null; // No container found
            }

            element = element.Parent;
        }

        return element;
    }




    private List<Relationship> GetRelationships(Func<Relationship, bool> filter)
    {
        if (_codeGraph is null)
        {
            return [];
        }

        return _codeGraph.GetAllRelationships().Where(filter).ToList();
    }

    private HashSet<Relationship> FindInheritsAndImplementsRelationships()
    {
        if (_codeGraph is null)
        {
            return [];
        }

        var inheritsAndImplements = new HashSet<Relationship>();
        _codeGraph.ForEachNode(Collect);
        return inheritsAndImplements;

        void Collect(CodeElement c)
        {
            if (c.ElementType is not (CodeElementType.Class or CodeElementType.Interface or CodeElementType.Struct or CodeElementType.Record))
            {
                return;
            }

            foreach (var relationship in c.Relationships)
            {
                if (relationship.Type is RelationshipType.Inherits or RelationshipType.Implements)
                {
                    inheritsAndImplements.Add(relationship);
                }
            }
        }
    }
}

public sealed class SearchResult
{
    public SearchResult(IEnumerable<CodeElement> elements, IEnumerable<Relationship> relationships)
    {
        // Ensure query is executed only once
        Elements = elements.ToList();
        Relationships = relationships.ToList();
    }

    public IReadOnlyList<CodeElement> Elements { get; }
    public IReadOnlyList<Relationship> Relationships { get; }
}