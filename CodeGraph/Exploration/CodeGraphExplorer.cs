using System.Diagnostics;
using CodeGraph.Algorithms.Cycles;
using CodeGraph.Graph;
using CSharpCodeAnalyst.Exploration;

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

        var existing = knownIds.ToArray();
        for (var i = 0; i < existing.Length; i++)
        {
            // We hit each pair twice so we walk only one direction here.
            var possibleChild = _codeGraph.Nodes[existing[i]];

            while (possibleChild.Parent is not null &&
                   CodeElementClassifier.GetContainerLevel(possibleChild.ElementType) == 0)
            {
                // We need a parent
                var parent = possibleChild.Parent;
                if (!knownIds.Contains(parent.Id))
                {
                    parentIds.Add(parent.Id);
                }

                possibleChild = possibleChild.Parent;
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

        var allCalls = GetRelationships(d => d.Type == RelationshipType.Calls);
        var calls = allCalls.Where(call => call.TargetId == method.Id).ToArray();
        var methods = calls.Select(d => _codeGraph.Nodes[d.SourceId]);

        return new SearchResult(methods, calls);
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
    /// </summary>
    public SearchResult FollowIncomingCallsHeuristically(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (_codeGraph is null)
        {
            return new SearchResult([], []);
        }

        var allImplementsAndOverrides =
            GetRelationships(d => d.Type is RelationshipType.Implements or RelationshipType.Overrides);
        var allCalls = GetRelationships(d => d.Type == RelationshipType.Calls);

        var allHandles = GetRelationships(d => d.Type == RelationshipType.Handles);
        var allInvokes = GetRelationships(d => d.Type == RelationshipType.Invokes);

        var method = _codeGraph.Nodes[id];

        var processingQueue = new PriorityQueue<(CodeElement, Context), int>();
        var initialContext = InitializeContextFromMethod(method);
        processingQueue.Enqueue((method, initialContext), 0); // Start with the initial method, priority 0

        var foundRelationships = new HashSet<Relationship>();
        var foundElements = new HashSet<CodeElement>
        {
            // For convenience. The element is already in the graph. But this way the result is consistent.
            _codeGraph.Nodes[id]
        };

        var processed = new HashSet<string>();
        while (processingQueue.Count > 0)
        {
            // 0 = highest priority
            var (element, context) = processingQueue.Dequeue();
            if (!processed.Add(element.Id))
            {
                continue;
            }

            var currentContext = context;

            if (element.ElementType == CodeElementType.Event)
            {
                // An event is raised by the specialization
                var specializations = allImplementsAndOverrides.Where(d => d.TargetId == element.Id).ToArray();
                foundRelationships.UnionWith(specializations);
                var specializedSources = specializations.Select(d => _codeGraph.Nodes[d.SourceId]).ToHashSet();
                foundElements.UnionWith(specializedSources);
                AddToProcessingQueue(specializedSources, currentContext, 0);

                // Add all methods that invoke the event
                var invokes = allInvokes.Where(call => call.TargetId == element.Id).ToArray();
                foundRelationships.UnionWith(invokes);
                var invokeSources = invokes.Select(d => _codeGraph.Nodes[d.SourceId]).ToHashSet();
                foundElements.UnionWith(invokeSources);
                AddToProcessingQueue(invokeSources, currentContext, 2);
            }


            if (element.ElementType == CodeElementType.Method)
            {
                // Calls
                var calls = allCalls.Where(call => call.TargetId == element.Id && currentContext.IsCallAllowed(call))
                    .ToArray();
                foundRelationships.UnionWith(calls);

                // We may restrict further paths
                foreach (var call in calls)
                {
                    var newContext = context;

                    var callSource = _codeGraph.Nodes[call.SourceId];
                    foundElements.Add(callSource);

                    if (call.HasAttribute(RelationshipAttribute.IsStaticCall) ||
                        call.HasAttribute(RelationshipAttribute.IsExtensionMethodCall))

                    {
                        // Starting on a new instance or static method we reset the context.
                        newContext = new Context(_codeGraph);
                    }

                    if (call.HasAttribute(RelationshipAttribute.IsInstanceCall))
                    {
                        // The new instance restricts the search.
                        newContext = new Context(_codeGraph);
                        newContext.ForbiddenCallSourcesInHierarchy.UnionWith(RestrictHierarchyCallSources(callSource));
                    }

                    if (call.HasAttribute(RelationshipAttribute.IsBaseCall))
                    {
                        // Call to own base class restricts the possible further calls.
                        newContext = currentContext.Clone();
                        newContext.ForbiddenCallSourcesInHierarchy.UnionWith(RestrictHierarchyCallSources(callSource));
                    }

                    AddToProcessingQueue([callSource], newContext, 0);
                }


                // Abstractions
                // For methods the abstractions like interfaces may be called.
                var abstractions = allImplementsAndOverrides.Where(d => d.SourceId == element.Id).ToArray();
                foundRelationships.UnionWith(abstractions);
                var abstractionTargets = abstractions.Select(d => _codeGraph.Nodes[d.TargetId]).ToHashSet();
                foundElements.UnionWith(abstractionTargets);
                AddToProcessingQueue(abstractionTargets, currentContext, 1);


                // Add Events that are handled by this method
                var handles = allHandles.Where(h => h.SourceId == element.Id).ToArray();
                foundRelationships.UnionWith(handles);
                var events = handles.Select(h => _codeGraph.Nodes[h.TargetId]).ToHashSet();
                foundElements.UnionWith(events);
                AddToProcessingQueue(events, currentContext, 2);
            }
        }

        return new SearchResult(foundElements, foundRelationships);


        void AddToProcessingQueue(IEnumerable<CodeElement> elementsToExplore, Context context, int priority)
        {
            foreach (var elementToExplore in elementsToExplore)
            {
                processingQueue.Enqueue((elementToExplore, context), priority);
            }
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
        processed.Clear();
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
        var methods = relationships.Select(m => _codeGraph.Nodes[m.SourceId]).ToList();
        return new SearchResult(methods, relationships);
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
        var methods = relationships.Select(m => _codeGraph.Nodes[m.TargetId]).ToList();
        return new SearchResult(methods, relationships);
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
        var methods = calls.Select(m => _codeGraph.Nodes[m.TargetId]).ToList();
        return new SearchResult(methods, calls);
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

        var relationships = _codeGraph.Nodes.Values
            .SelectMany(node => node.Relationships)
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
    ///     Returns all forbidden calls within the hierarchy.
    /// </summary>
    private HashSet<CodeElement> RestrictHierarchyCallSources(CodeElement someMethodInHierarchy)
    {
        if (_codeGraph is null)
        {
            return [];
        }

        var element = GetMethodContainer(someMethodInHierarchy);
        if (element is null)
        {
            return [];
        }

        var baseClasses = GetBaseClassesRecursive(element);

        // Non instance calls may originate from these classes.
        var allowedHierarchy = baseClasses
            .Union(GetDerivedClassesRecursive(element))
            .Union([element])
            .ToHashSet();

        // Side hierarchies originating from a shared base class.
        var expandedBaseClasses = new HashSet<CodeElement>();
        foreach (var baseClass in baseClasses)
        {
            expandedBaseClasses.UnionWith(FindFullInheritanceTree(baseClass.Id).Elements);
        }

        var forbiddenHierarchy = expandedBaseClasses.Except(allowedHierarchy).ToHashSet();
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
        while (element.ElementType != CodeElementType.Class
               && element.ElementType != CodeElementType.Struct
               && element.ElementType != CodeElementType.Interface
               && element.ElementType != CodeElementType.Record)
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

    private Context InitializeContextFromMethod(CodeElement method)
    {
        var context = new Context(_codeGraph!)
        {
            ForbiddenCallSourcesInHierarchy = RestrictHierarchyCallSources(method)
        };
        return context;
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

public record struct SearchResult
{
    public SearchResult(IEnumerable<CodeElement> elements, IEnumerable<Relationship> relationships)
    {
        // Ensure query is executed only once
        Elements = elements.ToList();
        Relationships = relationships.ToList();
    }

    public IEnumerable<CodeElement> Elements { get; set; }
    public IEnumerable<Relationship> Relationships { get; set; }
}