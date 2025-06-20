using System.Diagnostics;
using CodeParser.Analysis.Cycles;
using Contracts.Graph;

namespace CSharpCodeAnalyst.Exploration;

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
    ///     This gives a heuristic only. The graph model does not contain the information for analyzing dynamic behavior.
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

        var restriction = new FollowIncomingCallsRestriction();
       var processingQueue = new PriorityQueue<CodeElement, int>();
        processingQueue.Enqueue(method, 0); // Start with the initial method, priority 0

        var foundRelationships = new HashSet<Relationship>();
        var foundElements = new HashSet<CodeElement>();

        // For convenience. The element is already in the graph. But this way the result is consistent.
        foundElements.Add(_codeGraph.Nodes[id]);


        var processed = new HashSet<string>();
        while (processingQueue.Count > 0)
        {
            // 0 = highest priority

            var element = processingQueue.Dequeue();
            if (!processed.Add(element.Id))
            {
                continue;
            }


            if (element.ElementType == CodeElementType.Event)
            {
                // An event is raised by the specialization
                var specializations = allImplementsAndOverrides.Where(d => d.TargetId == element.Id).ToArray();
                foundRelationships.UnionWith(specializations);
                var specializedSources = specializations.Select(d => _codeGraph.Nodes[d.SourceId]).ToHashSet();
                foundElements.UnionWith(specializedSources);
                AddToProcessingQueue(specializedSources, 0);

                // Add all methods that invoke the event
                var invokes = allInvokes.Where(call => call.TargetId == element.Id).ToArray();
                foundRelationships.UnionWith(invokes);
                var invokeSources = invokes.Select(d => _codeGraph.Nodes[d.SourceId]).ToHashSet();
                foundElements.UnionWith(invokeSources);
                AddToProcessingQueue(invokeSources, 2);
            }

            if (element.ElementType == CodeElementType.Method)
            {
                // 1. Abstractions (priority 0)
                // The abstractions limit the allowed calls.
                // For methods the abstractions like interfaces may be called.
                var abstractions = allImplementsAndOverrides.Where(d => d.SourceId == element.Id).ToArray();
                foundRelationships.UnionWith(abstractions);
                var abstractionTargets = abstractions.Select(d => _codeGraph.Nodes[d.TargetId]).ToHashSet();
                foundElements.UnionWith(abstractionTargets);
                restriction.BlockeBaseCalls.UnionWith(abstractionTargets.Select(t => t.Id));
                AddToProcessingQueue(abstractionTargets, 0);


                // Add Events that are handled by this method  (priority 1).
                var handles = allHandles.Where(h => h.SourceId == element.Id).ToArray();
                foundRelationships.UnionWith(handles);
                var events = handles.Select(h => _codeGraph.Nodes[h.TargetId]).ToHashSet();
                foundElements.UnionWith(events);
                AddToProcessingQueue(events, 1);
    

                // 3. Calls (priority 2)
                var calls = allCalls.Where(call => call.TargetId == element.Id && IsAllowedCall(call)).ToArray();
                foundRelationships.UnionWith(calls);
                var callSources = calls
                    .Select(d => _codeGraph.Nodes[d.SourceId])
                    .ToHashSet();
                foundElements.UnionWith(callSources);
                AddToProcessingQueue(callSources, 2);
            }
        }

        return new SearchResult(foundElements, foundRelationships);

        void AddToProcessingQueue(IEnumerable<CodeElement> elementsToExplore, int priority)
        {
            foreach (var elementToExplore in elementsToExplore)
            {
                processingQueue.Enqueue(_codeGraph.Nodes[elementToExplore.Id], priority);
            }
        }
        
        bool IsAllowedCall(Relationship call)
        {
            if (restriction.BlockeBaseCalls.Contains(call.TargetId))
            {
                var allow = !IsCallToOwnBase(call);
                if (!allow)
                {
                    var sourceName = _codeGraph.Nodes[call.SourceId].FullName;
                    var targetName = _codeGraph.Nodes[call.TargetId].FullName;
                    Trace.WriteLine($"Removed: {sourceName} ->  {targetName}");
                }

                return allow;
            }

            return true;
        }
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

        // Find sub-classes recursive
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


    /// <summary>
    ///     source --> target (abstract)
    /// </summary>
    private bool IsCallToOwnBase(Relationship call)
    {
        // Is target more abstract than source?
        // target  (abstract) <-- source
        var isCallToBaseClass = GetRelationships(d => d.Type is RelationshipType.Overrides)
            .Any(r => r.SourceId == call.SourceId && r.TargetId == call.TargetId);
        return isCallToBaseClass;
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

    private class FollowIncomingCallsRestriction
    {
        /// <summary>
        ///     If we follow incoming calls we include the abstraction of the method.
        ///     This is because the method may be indirectly called by the interface.
        ///     If we proceed we may also find calls the base. But this is the wrong direction for the path we follow.
        ///     So if we followed an abstraction we block the base call to this abstraction
        ///
        ///     <code>
        ///     class Base
        ///     {
        ///         protected virtual void Foo() {}
        ///     }
        ///
        ///     class Derived : Base
        ///     {
        ///         // We start following here! 
        ///         protected override void Foo()
        ///         {
        ///             base.Foo()
        ///         }
        ///     }
        ///     </code>
        ///
        ///     Hashset of target ids of base methods.
        /// </summary>
        public HashSet<string> BlockeBaseCalls { get; } = [];
        
        public HashSet<string> BlockedAbstraction { get; } = [];
    }
}

public record struct SearchResult(IEnumerable<CodeElement> Elements, IEnumerable<Relationship> Relationships);

public record struct Invocation(IEnumerable<CodeElement> Methods, IEnumerable<Relationship> Calls);