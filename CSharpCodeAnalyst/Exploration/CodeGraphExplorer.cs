using CodeParser.Analysis.Cycles;
using Contracts.Graph;

namespace CSharpCodeAnalyst.Exploration;

public class CodeGraphExplorer : ICodeGraphExplorer
{
    private List<Dependency> _allDependencies = [];
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
    public IEnumerable<Dependency> FindAllDependencies(HashSet<string> ids)
    {
        if (_codeGraph is null)
        {
            return [];
        }

        var dependencies = _codeGraph.Nodes.Values
            .SelectMany(n => n.Dependencies)
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

        var allCalls = GetDependencies(d => d.Type == DependencyType.Calls);
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

        var foundCalls = new HashSet<Dependency>();
        var foundMethods = new HashSet<CodeElement>();

        var allCalls = GetDependencies(d => d.Type == DependencyType.Calls);

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
            GetDependencies(d => d.Type is DependencyType.Implements or DependencyType.Overrides);
        var allCalls = GetDependencies(d => d.Type == DependencyType.Calls);

        var method = _codeGraph.Nodes[id];

        var processingQueue = new Queue<CodeElement>();
        processingQueue.Enqueue(method);

        var foundDependencies = new HashSet<Dependency>();
        var foundElements = new HashSet<CodeElement>();


        var processed = new HashSet<string>();
        while (processingQueue.Any())
        {
            var element = processingQueue.Dequeue();
            if (!processed.Add(element.Id))
            {
                continue;
            }

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
            var methodsToExplore = abstractionTargets.Union(callSources);
            foreach (var methodToExplore in methodsToExplore)
            {
                processingQueue.Enqueue(_codeGraph.Nodes[methodToExplore.Id]);
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
        var relationships = new HashSet<Dependency>();
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
                typeToAnalyze.Dependencies.Where(d => d.Type is DependencyType.Implements or DependencyType.Inherits);
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

        var dependencies = _codeGraph.GetAllDependencies()
            .Where(d => (d.Type == DependencyType.Overrides ||
                         d.Type == DependencyType.Implements) &&
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

        var dependencies = element.Dependencies
            .Where(d => (d.Type == DependencyType.Overrides ||
                         d.Type == DependencyType.Implements) &&
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

        var calls = method.Dependencies
            .Where(d => d.Type == DependencyType.Calls).ToList();
        var methods = calls.Select(m => _codeGraph.Nodes[m.TargetId]).ToList();
        return new Invocation(methods, calls);
    }

    public SearchResult FindOutgoingDependencies(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (_codeGraph is null)
        {
            return new SearchResult([], []);
        }

        var element = _codeGraph.Nodes[id];
        var dependencies = element.Dependencies;
        var targets = dependencies.Select(m => _codeGraph.Nodes[m.TargetId]).ToList();
        return new SearchResult(targets, dependencies);
    }

    public SearchResult FindIncomingDependencies(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (_codeGraph is null)
        {
            return new SearchResult([], []);
        }

        var element = _codeGraph.Nodes[id];
        var dependencies = _codeGraph.Nodes.Values
            .SelectMany(node => node.Dependencies)
            .Where(d => d.TargetId == element.Id).ToList();

        var elements = dependencies.Select(d => _codeGraph.Nodes[d.SourceId]);

        return new SearchResult(elements, dependencies);
    }

    private List<Dependency> GetCachedDependencies()
    {
        if (_codeGraph is null)
        {
            return [];
        }

        if (_allDependencies.Count == 0)
        {
            _allDependencies = _codeGraph.GetAllDependencies().ToList();
        }

        return _allDependencies;
    }

    private List<Dependency> GetDependencies(Func<Dependency, bool> filter)
    {
        return GetCachedDependencies().Where(filter).ToList();
    }

    private HashSet<Dependency> FindInheritsAndImplementsRelationships()
    {
        if (_codeGraph is null)
        {
            return [];
        }

        var inheritsAndImplements = new HashSet<Dependency>();
        _codeGraph.DfsHierarchy(Collect);
        return inheritsAndImplements;

        void Collect(CodeElement c)
        {
            if (c.ElementType is not (CodeElementType.Class or CodeElementType.Interface or CodeElementType.Struct))
            {
                return;
            }

            foreach (var dependency in c.Dependencies)
            {
                if (dependency.Type is DependencyType.Inherits or DependencyType.Implements)
                {
                    inheritsAndImplements.Add(dependency);
                }
            }
        }
    }
}

public record struct SearchResult(IEnumerable<CodeElement> Elements, IEnumerable<Dependency> Dependencies);

public record struct Invocation(IEnumerable<CodeElement> Methods, IEnumerable<Dependency> Calls);