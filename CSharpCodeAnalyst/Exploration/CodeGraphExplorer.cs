using System.Diagnostics;
using System.Security.Policy;
using CodeParser.Analysis.Cycles;
using Contracts.Graph;

namespace CSharpCodeAnalyst.Exploration;

internal class Context(CodeGraph _codeGraph)
{
    public HashSet<CodeElement> ForbiddenCallSourcesInHierarchy { get; set; } = [];

    public bool IsCallAllowed(Relationship call)
    {
        if (ForbiddenCallSourcesInHierarchy.Count == 0)
        {
            return true; // No restrictions, all calls are allowed
        }

        var source = _codeGraph.Nodes[call.SourceId];
        var target = _codeGraph.Nodes[call.TargetId];

        if (call.Attributes == RelationshipAttribute.None || call.HasAttribute(RelationshipAttribute.IsBaseCall))
        {
            // Normal calls not a call to an object instance.
            var sourceMethod = _codeGraph.Nodes[call.SourceId];
            var sourceClass = CodeGraphExplorer.GetMethodContainer(sourceMethod);

            if (ForbiddenCallSourcesInHierarchy.Contains(sourceClass))
            {
                return false;
            }
        }

        return true;
    }

    public Context Clone()
    {
        var clone = new Context(_codeGraph)
        {
            ForbiddenCallSourcesInHierarchy = ForbiddenCallSourcesInHierarchy.ToHashSet()
        };
        return clone;
    }
}

public class CodeGraphExplorer : ICodeGraphExplorer
{
    private List<Relationship> _allRelationships = [];
    private CodeGraph? _codeGraph;

    public void LoadCodeGraph(CodeGraph graph)
    {
        _codeGraph = graph;

        // Clear all cached data
        _allRelationships = [];
    }

    public List<CodeElement> GetElements(List<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (_codeGraph is null)
        {
            return [];
        }

        List<CodeElement> elements = new();
        foreach (var id in ids)
        {
            if (_codeGraph.Nodes.TryGetValue(id, out var element))
                // The element is cloned internally and the relationships discarded.
            {
                elements.Add(element);
            }
        }

        return elements;
    }

    public SearchResult CompleteToContainingTypes(HashSet<string> ids)
    {
        if (_codeGraph is null)
        {
            return new SearchResult([], []);
        }

        var parents = new List<CodeElement>();

        foreach (var id in ids)
        {
            if (_codeGraph.Nodes.TryGetValue(id, out var element))
            {
                var current = element;
                while (current.Parent is not null &&
                       CodeElementClassifier.GetContainerLevel(current.ElementType) == 0)
                {
                    // We need a parent
                    var parent = current.Parent;
                    if (ids.Contains(parent.Id) is false)
                    {
                        parents.Add(parent);
                    }

                    current = current.Parent;
                }
            }
        }

        return new SearchResult(parents, []);
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

        var relationships = _codeGraph.Nodes.Values
            .SelectMany(n => n.Relationships)
            .Where(d => ids.Contains(d.SourceId) && ids.Contains(d.TargetId))
            .ToList();

        return relationships;
    }

    public Invocation FindIncomingCalls(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (_codeGraph is null)
        {
            return new Invocation([], []);
        }

        var method = _codeGraph.Nodes[id];

        var allCalls = GetRelationships(d => d.Type == RelationshipType.Calls);
        var calls = allCalls.Where(call => call.TargetId == method.Id).ToArray();
        var methods = calls.Select(d => _codeGraph.Nodes[d.SourceId]);

        return new Invocation(methods, calls);
    }


    public Invocation FindIncomingCallsRecursive(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (_codeGraph is null)
        {
            return new Invocation([], []);
        }

        var method = _codeGraph.Nodes[id];

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
                processingQueue.Enqueue(_codeGraph.Nodes[methodToExplore.Id]);
            }
        }

        return new Invocation(foundMethods, foundCalls);
    }

    /// <summary>
    /// Returns all allowed call sources within the hierarchy.
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
    /// The given element is not included in the result.
    /// </summary>
    private HashSet<CodeElement> GetBaseClassesRecursive(CodeElement? element)
    {
        if (_codeGraph is null)
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
                .Where(d => d.Type == RelationshipType.Inherits && d.SourceId == element.Id)
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

    public static CodeElement? GetMethodContainer(CodeElement element)
    {
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

        var derivedClasses = new HashSet<CodeElement>();

        var queue = new Queue<CodeElement>();
        queue.Enqueue(element);

        while (queue.Any())
        {
            var currentElement = queue.Dequeue();

            var derived = GetRelationships(d => d.Type == RelationshipType.Inherits && d.TargetId == element.Id)
                .Select(m => _codeGraph.Nodes[m.SourceId]).ToList();

         foreach (var baseClass in derived)
            {
                if (derivedClasses.Add(baseClass))
                {
                    queue.Enqueue(baseClass);
                }
            }
        }

        return derivedClasses;
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
        var foundElements = new HashSet<CodeElement>();

        // For convenience. The element is already in the graph. But this way the result is consistent.
        foundElements.Add(_codeGraph.Nodes[id]);

        var dbg = 0;
        var processed = new HashSet<string>();
        while (processingQueue.Count > 0)
        {
            dbg++;
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
                var calls = allCalls.Where(call => call.TargetId == element.Id && currentContext.IsCallAllowed(call)).ToArray();
                foundRelationships.UnionWith(calls);

                // We may restrict further paths
                foreach (var call in calls)
                {
                    var callSource = _codeGraph.Nodes[call.SourceId];
                    foundElements.Add(callSource);

                    if (call.HasAttribute(RelationshipAttribute.IsInstanceCall) ||
                        call.HasAttribute(RelationshipAttribute.IsStaticCall) ||
                        call.HasAttribute(RelationshipAttribute.IsExtensionMethodCall))

                    {
                        // Starting on a new instance or static method we reset the context.
                        currentContext = new Context(_codeGraph);
                    }

                    if (call.Attributes == RelationshipAttribute.IsBaseCall)
                    {
                        // Call to own base class restricts the possible further calls.
                        currentContext = currentContext.Clone();
                        currentContext.ForbiddenCallSourcesInHierarchy.UnionWith(RestrictHierarchyCallSources(callSource));
                    }

                    AddToProcessingQueue([callSource], currentContext, 0);
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
                processingQueue.Enqueue((_codeGraph.Nodes[elementToExplore.Id], context), priority);
            }
        }

    }

    private Context InitializeContextFromMethod(CodeElement method)
    {
        var context = new Context(_codeGraph)
        {
            ForbiddenCallSourcesInHierarchy = RestrictHierarchyCallSources(method)
        };
        return context;
    }

    /// <summary>
    ///     subclass -- inherits--> baseclass
    /// </summary>
    public SearchResult FindFullInheritanceTree(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (_codeGraph is null)
        {
            return new SearchResult([], []);
        }

        var type = _codeGraph.Nodes[id];

        var types = new HashSet<CodeElement>();
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
                // Since we evaluate both direction in one iteration, an already processed node is added again.
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

        if (_codeGraph is null)
        {
            return new SearchResult([], []);
        }

        var element = _codeGraph.Nodes[id];

        var relationships = _codeGraph.GetAllRelationships()
            .Where(d => (d.Type == RelationshipType.Overrides ||
                         d.Type == RelationshipType.Implements) &&
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

        if (_codeGraph is null)
        {
            return new SearchResult([], []);
        }

        var element = _codeGraph.Nodes[id];

        var relationships = element.Relationships
            .Where(d => (d.Type == RelationshipType.Overrides ||
                         d.Type == RelationshipType.Inherits ||
                         d.Type == RelationshipType.Implements) &&
                        d.SourceId == element.Id).ToList();
        var methods = relationships.Select(m => _codeGraph.Nodes[m.TargetId]).ToList();
        return new SearchResult(methods, relationships);
    }


    public Invocation FindOutgoingCalls(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (_codeGraph is null)
        {
            return new Invocation([], []);
        }

        var method = _codeGraph.Nodes[id];

        var calls = method.Relationships
            .Where(d => d.Type == RelationshipType.Calls).ToList();
        var methods = calls.Select(m => _codeGraph.Nodes[m.TargetId]).ToList();
        return new Invocation(methods, calls);
    }

    public SearchResult FindOutgoingRelationships(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (_codeGraph is null)
        {
            return new SearchResult([], []);
        }

        var element = _codeGraph.Nodes[id];
        var relationships = element.Relationships;
        var targets = relationships.Select(m => _codeGraph.Nodes[m.TargetId]).ToList();
        return new SearchResult(targets, relationships);
    }

    public SearchResult FindIncomingRelationships(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (_codeGraph is null)
        {
            return new SearchResult([], []);
        }

        var element = _codeGraph.Nodes[id];
        var relationships = _codeGraph.Nodes.Values
            .SelectMany(node => node.Relationships)
            .Where(d => d.TargetId == element.Id).ToList();

        var elements = relationships.Select(d => _codeGraph.Nodes[d.SourceId]);

        return new SearchResult(elements, relationships);
    }



    private List<Relationship> GetCachedRelationships()
    {
        if (_codeGraph is null)
        {
            return [];
        }

        if (_allRelationships.Count == 0)
        {
            _allRelationships = _codeGraph.GetAllRelationships().ToList();
        }

        return _allRelationships;
    }

    private List<Relationship> GetRelationships(Func<Relationship, bool> filter)
    {
        return GetCachedRelationships().Where(filter).ToList();
    }

    private HashSet<Relationship> FindInheritsAndImplementsRelationships()
    {
        if (_codeGraph is null)
        {
            return [];
        }

        var inheritsAndImplements = new HashSet<Relationship>();
        _codeGraph.DfsHierarchy(Collect);
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

public record struct SearchResult(IEnumerable<CodeElement> Elements, IEnumerable<Relationship> Relationships);

public record struct Invocation(IEnumerable<CodeElement> Methods, IEnumerable<Relationship> Calls);