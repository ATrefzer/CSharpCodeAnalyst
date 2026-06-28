using CodeGraph.Algorithms.Cycles;
using CodeGraph.Contracts;
using CodeGraph.Graph;
using System.Diagnostics;

namespace CodeGraph.Exploration;

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

    public SearchResult FindOutgoingRelationshipsDeep(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (_codeGraph is null || !_codeGraph.Nodes.TryGetValue(id, out var element))
        {
            return new SearchResult([], []);
        }

        var sources = element.GetChildrenIncludingSelf();
        var relationships = _codeGraph.GetAllRelationships().Where(r => sources.Contains(r.SourceId)).ToList();
        var targets = relationships.Select(m => m.TargetId).ToList();

        var elements = sources.Union(targets).Select(i => _codeGraph.Nodes[i]).ToHashSet();

        return new SearchResult(elements, relationships);
    }

    public SearchResult FindIncomingRelationshipsDeep(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (_codeGraph is null || !_codeGraph.Nodes.TryGetValue(id, out var element))
        {
            return new SearchResult([], []);
        }

        var targetIds = element.GetChildrenIncludingSelf();
        var relationships = _codeGraph.GetAllRelationships().Where(r => targetIds.Contains(r.TargetId)).ToList();
        var sources = relationships.Select(d => d.SourceId);
        var elements = sources.Union(targetIds).Select(i => _codeGraph.Nodes[i]).ToHashSet();

        return new SearchResult(elements, relationships);
    }

    public SearchResult FindParents(List<string> ids)
    {
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

        var allCalls = GetRelationships(d => d.Type == RelationshipType.Calls);

        var processed = new HashSet<string>();
        while (processingQueue.Any())
        {
            var element = processingQueue.Dequeue();
            if (!processed.Add(element.Id))
            {
                continue;
            }

            var calls = allCalls.Where(call => call.TargetId == element.Id).ToArray();
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

        var allImplementsAndOverrides =
            GetRelationships(d => d.Type is RelationshipType.Implements or RelationshipType.Overrides);
        var allCalls = GetRelationships(d => d.Type == RelationshipType.Calls);
        var allHandles = GetRelationships(d => d.Type == RelationshipType.Handles);
        var allInvokes = GetRelationships(d => d.Type == RelationshipType.Invokes);

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
                var specializedEvents = Collect(allImplementsAndOverrides.Where(d => d.TargetId == element.Id), d => d.SourceId);
                AddToProcessingQueue(specializedEvents, context);

                // The publisher side is unrelated to
                // the subscriber's hierarchy, so continue as if the search started at the invoker.
                // Raising the event does not use virtual dispatch on "this": the handlers were
                // bound at registration time and are reached through the delegate's invocation list.
                var invokers = Collect(allInvokes.Where(d => d.TargetId == element.Id), d => d.SourceId);
                foreach (var invoker in invokers)
                {
                    AddToProcessingQueue([invoker], CreateContextForMethod(invoker));
                }
            }

            // Properties take part in call chains like methods do:
            // accessor bodies are call sources and property accesses are modeled as calls.
            // When property accessors are split, the get_/set_ elements carry those calls instead.
            if (element.ElementType is CodeElementType.Method or CodeElementType.Property
                or CodeElementType.PropertyAccessor)
            {
                // Follow the callers whose calls can dispatch to this element under the
                // current restrictions. Each caller gets the context for its kind of call.
                var calls = allCalls.Where(call => call.TargetId == element.Id && context.IsCallAllowed(call));
                foreach (var call in calls)
                {
                    foundRelationships.Add(call);
                    var caller = graph.Nodes[call.SourceId];
                    foundElements.Add(caller);
                    AddToProcessingQueue([caller], CreateContextForCaller(call, caller, context));
                }

                // The abstractions (interface or base declarations) may be called instead of
                // the concrete element. Follow them with the current restrictions.
                var abstractions = Collect(allImplementsAndOverrides.Where(d => d.SourceId == element.Id), d => d.TargetId);
                AddToProcessingQueue(abstractions, context);

                // If this element handles an event, the chain continues at the event.
                var handledEvents = Collect(allHandles.Where(h => h.SourceId == element.Id), d => d.TargetId);
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
                restricted.ForbiddenCallSourcesInHierarchy.UnionWith(GetForbiddenCallSourcesInHierarchy(caller));
                return restricted;
            }

            if (call.HasAttribute(RelationshipAttribute.IsStaticCall) ||
                call.HasAttribute(RelationshipAttribute.IsExtensionMethodCall) ||
                call.HasAttribute(RelationshipAttribute.IsInstanceCall))
            {
                // The call breaks the dispatch chain of the current "this" instance.
                // Continue as if the search started at the caller, restricted to its hierarchy.
                return CreateContextForMethod(caller);
            }

            // Implicit and "this" calls stay within the current dispatch chain.
            return context;
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

        // For completeness add the start type.
        var types = new HashSet<CodeElement> { type };

        var relationships = new HashSet<Relationship>();
        var processingQueue = new Queue<CodeElement>();
        var processed = new HashSet<string>();

        var inheritsAndImplements = FindInheritsAndImplementsRelationships();

        // Find base classes recursive
        processingQueue.Enqueue(type);
        while (processingQueue.Any())
        {
            var typeToAnalyze = processingQueue.Dequeue();
            if (!processed.Add(typeToAnalyze.Id))
            {
                continue;
            }

            // Case typeToAnalyze is subclass: typeToAnalyze implements X or inherits from Y
            var abstractionsOfAnalyzedType =
                typeToAnalyze.Relationships.Where(d =>
                    d.Type is RelationshipType.Implements or RelationshipType.Inherits);
            foreach (var abstraction in abstractionsOfAnalyzedType)
            {
                var baseType = _codeGraph.Nodes[abstraction.TargetId];
                types.Add(baseType);
                relationships.Add(abstraction);
                processingQueue.Enqueue(baseType);
            }
        }

        // Find subclasses recursively
        processingQueue.Enqueue(type);
        processed.Clear();
        while (processingQueue.Any())
        {
            var typeToAnalyze = processingQueue.Dequeue();
            if (!processed.Add(typeToAnalyze.Id))
            {
                continue;
            }

            // Case typeToAnalyze is base class: typeToAnalyze is implemented by X or Y inherits from it.
            var specializationsOfAnalyzedType
                = inheritsAndImplements.Where(d => typeToAnalyze.Id == d.TargetId);
            foreach (var specialization in specializationsOfAnalyzedType)
            {
                var specializedType = _codeGraph.Nodes[specialization.SourceId];

                types.Add(specializedType);
                relationships.Add(specialization);
                processingQueue.Enqueue(specializedType);
            }
        }

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
    ///     reaching the given method: the side branches of its class hierarchy.
    /// </summary>
    private HashSet<CodeElement> GetForbiddenCallSourcesInHierarchy(CodeElement method)
    {
        if (_codeGraph is null)
        {
            return [];
        }

        var container = GetMethodContainer(method);
        if (container is null)
        {
            return [];
        }

        var baseClasses = GetBaseClassesRecursive(container);

        // Non instance calls may originate from these classes.
        var allowedHierarchy = baseClasses
            .Union(GetDerivedClassesRecursive(container))
            .Union([container])
            .ToHashSet();

        // Side hierarchies originating from a shared base class.
        var expandedBaseClasses = new HashSet<CodeElement>();
        foreach (var baseClass in baseClasses)
        {
            expandedBaseClasses.UnionWith(FindFullInheritanceTree(baseClass.Id).Elements);
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

    /// <summary>
    ///     The given element is not included in the result.
    /// </summary>
    private HashSet<CodeElement> GetBaseClassesRecursive(CodeElement? element)
    {
        if (_codeGraph is null || element is null)
        {
            return [];
        }

        var baseClasses = new HashSet<CodeElement>();

        var queue = new Queue<CodeElement>();
        queue.Enqueue(element);

        while (queue.Any())
        {
            var currentElement = queue.Dequeue();

            var inheritsFrom = currentElement.Relationships
                .Where(d => d.Type == RelationshipType.Inherits && d.SourceId == currentElement.Id)
                .Select(m => _codeGraph.Nodes[m.TargetId]).ToList();

            Debug.Assert(inheritsFrom.Count <= 1, "Only simple inheritance in C#");

            foreach (var baseClass in inheritsFrom)
            {
                if (baseClasses.Add(baseClass))
                {
                    queue.Enqueue(baseClass);
                }
            }
        }

        return baseClasses;
    }

    public static CodeElement? GetMethodContainer(CodeElement method)
    {
        var element = method;
        while (element.ElementType is not (CodeElementType.Class or CodeElementType.Struct
               or CodeElementType.Interface or CodeElementType.Record))
        {
            if (element.Parent is null)
            {
                return null; // No container found
            }

            element = element.Parent;
        }

        return element;
    }


    private HashSet<CodeElement> GetDerivedClassesRecursive(CodeElement element)
    {
        if (_codeGraph is null)
        {
            return [];
        }

        var result = new HashSet<CodeElement>();

        var queue = new Queue<CodeElement>();
        queue.Enqueue(element);

        while (queue.Any())
        {
            var currentElement = queue.Dequeue();

            var derivedClasses =
                GetRelationships(d => d.Type == RelationshipType.Inherits && d.TargetId == currentElement.Id)
                    .Select(m => _codeGraph.Nodes[m.SourceId]).ToList();

            foreach (var derived in derivedClasses)
            {
                if (result.Add(derived))
                {
                    queue.Enqueue(derived);
                }
            }
        }

        return result;
    }

    /// <summary>
    ///     Creates the context for exploring the callers of the given method, as if the
    ///     search started there.
    /// </summary>
    private Context CreateContextForMethod(CodeElement method)
    {
        return new Context(_codeGraph!)
        {
            ForbiddenCallSourcesInHierarchy = GetForbiddenCallSourcesInHierarchy(method)
        };
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
            if (c.ElementType is not (CodeElementType.Class or CodeElementType.Interface or CodeElementType.Struct))
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

public readonly record struct SearchResult
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