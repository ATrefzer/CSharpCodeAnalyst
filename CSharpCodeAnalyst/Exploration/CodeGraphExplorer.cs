using CodeParser.Analysis.Cycles;
using Contracts.Graph;

namespace CSharpCodeAnalyst.Exploration;

public class CodeGraphExplorer : ICodeGraphExplorer
{
    private List<Relationship> _allDependencies = [];
    private CodeGraph? _codeGraph;

    public void LoadCodeGraph(CodeGraph graph)
    {
        _codeGraph = graph;

        // Clear all cached data
        _allDependencies = [];
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
            {
                // The element is cloned internally and the dependencies discarded.
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
    ///     Returns all dependencies that link the given nodes (ids).
    /// </summary>
    public IEnumerable<Relationship> FindAllDependencies(HashSet<string> ids)
    {
        if (_codeGraph is null)
        {
            return [];
        }

        var dependencies = _codeGraph.Nodes.Values
            .SelectMany(n => n.Relationships)
            .Where(d => ids.Contains(d.SourceId) && ids.Contains(d.TargetId))
            .ToList();

        return dependencies;
    }

    public Invocation FindIncomingCalls(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (_codeGraph is null)
        {
            return new Invocation([], []);
        }

        var method = _codeGraph.Nodes[id];

        var allCalls = GetDependencies(d => d.Type == RelationshipType.Calls);
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

        var allCalls = GetDependencies(d => d.Type == RelationshipType.Calls);

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


    public SearchResult FollowIncomingCallsRecursive(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (_codeGraph is null)
        {
            return new SearchResult([], []);
        }

        var allImplementsAndOverrides =
            GetDependencies(d => d.Type is RelationshipType.Implements or RelationshipType.Overrides);
        var allCalls = GetDependencies(d => d.Type == RelationshipType.Calls);

        var allHandles = GetDependencies(d => d.Type == RelationshipType.Handles);
        var allInvokes = GetDependencies(d => d.Type == RelationshipType.Invokes);

        var method = _codeGraph.Nodes[id];

        var processingQueue = new Queue<CodeElement>();
        processingQueue.Enqueue(method);

        var foundDependencies = new HashSet<Relationship>();
        var foundElements = new HashSet<CodeElement>();


        var processed = new HashSet<string>();
        while (processingQueue.Any())
        {
            var element = processingQueue.Dequeue();
            if (!processed.Add(element.Id))
            {
                continue;
            }

            // An event is raised by the specialization
            var specializations = allImplementsAndOverrides.Where(d => d.TargetId == element.Id).ToArray();
            foundDependencies.UnionWith(specializations);
            var specializedSources = specializations.Select(d => _codeGraph.Nodes[d.SourceId]).ToHashSet();
            foundElements.UnionWith(specializedSources);

            // Add all methods that invoke the event
            var invokes = allInvokes.Where(call => call.TargetId == element.Id).ToArray();
            foundDependencies.UnionWith(invokes);
            var invokeSources = invokes.Select(d => _codeGraph.Nodes[d.SourceId]).ToHashSet();
            foundElements.UnionWith(invokeSources);

            // Add Events that are handled by this method.
            var handles = allHandles.Where(h => h.SourceId == element.Id).ToArray();
            foundDependencies.UnionWith(handles);
            var events = handles.Select(h => _codeGraph.Nodes[h.TargetId]).ToHashSet();
            foundElements.UnionWith(events);

            // Calls
            var calls = allCalls.Where(call => call.TargetId == element.Id).ToArray();
            foundDependencies.UnionWith(calls);
            var callSources = calls.Select(d => _codeGraph.Nodes[d.SourceId]).ToHashSet();
            foundElements.UnionWith(callSources);

            // Abstractions. Sometimes the abstractions is called.
            var abstractions = allImplementsAndOverrides.Where(d => d.SourceId == element.Id).ToArray();
            foundDependencies.UnionWith(abstractions);
            var abstractionTargets = abstractions.Select(d => _codeGraph.Nodes[d.TargetId]).ToHashSet();
            foundElements.UnionWith(abstractionTargets);

            // Follow new leads
            var elementsToExplore = abstractionTargets
                .Union(callSources)
                .Union(events)
                .Union(invokeSources)
                .Union(specializedSources);
            foreach (var elementToExplore in elementsToExplore)
            {
                processingQueue.Enqueue(_codeGraph.Nodes[elementToExplore.Id]);
            }
        }

        return new SearchResult(foundElements, foundDependencies);
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
            {
                // Since we evaluate both direction in one iteration, an already processed node is added again.
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

        var dependencies = _codeGraph.GetAllRelationships()
            .Where(d => (d.Type == RelationshipType.Overrides ||
                         d.Type == RelationshipType.Implements) &&
                        d.TargetId == element.Id).ToList();
        var methods = dependencies.Select(m => _codeGraph.Nodes[m.SourceId]).ToList();
        return new SearchResult(methods, dependencies);
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

        var dependencies = element.Relationships
            .Where(d => (d.Type == RelationshipType.Overrides ||
                         d.Type == RelationshipType.Implements) &&
                        d.SourceId == element.Id).ToList();
        var methods = dependencies.Select(m => _codeGraph.Nodes[m.TargetId]).ToList();
        return new SearchResult(methods, dependencies);
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
        var dependencies = element.Relationships;
        var targets = dependencies.Select(m => _codeGraph.Nodes[m.TargetId]).ToList();
        return new SearchResult(targets, dependencies);
    }

    public SearchResult FindIncomingRelationships(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (_codeGraph is null)
        {
            return new SearchResult([], []);
        }

        var element = _codeGraph.Nodes[id];
        var dependencies = _codeGraph.Nodes.Values
            .SelectMany(node => node.Relationships)
            .Where(d => d.TargetId == element.Id).ToList();

        var elements = dependencies.Select(d => _codeGraph.Nodes[d.SourceId]);

        return new SearchResult(elements, dependencies);
    }

    private List<Relationship> GetCachedDependencies()
    {
        if (_codeGraph is null)
        {
            return [];
        }

        if (_allDependencies.Count == 0)
        {
            _allDependencies = _codeGraph.GetAllRelationships().ToList();
        }

        return _allDependencies;
    }

    private List<Relationship> GetDependencies(Func<Relationship, bool> filter)
    {
        return GetCachedDependencies().Where(filter).ToList();
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

            foreach (var dependency in c.Relationships)
            {
                if (dependency.Type is RelationshipType.Inherits or RelationshipType.Implements)
                {
                    inheritsAndImplements.Add(dependency);
                }
            }
        }
    }
}

public record struct SearchResult(IEnumerable<CodeElement> Elements, IEnumerable<Relationship> Relationships);

public record struct Invocation(IEnumerable<CodeElement> Methods, IEnumerable<Relationship> Calls);