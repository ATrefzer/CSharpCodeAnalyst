using Contracts.Graph;

namespace CSharpCodeAnalyst.Exploration;

public class CodeGraphExplorer : ICodeGraphExplorer
{
    public Invocation FindIncomingCalls(CodeGraph codeGraph, CodeElement method)
    {
        ArgumentNullException.ThrowIfNull(codeGraph);
        ArgumentNullException.ThrowIfNull(method);

        var callDependencies = GetCallDependencies(codeGraph);
        var calls = callDependencies.Where(call => call.TargetId == method.Id).ToArray();
        var methods = calls.Select(d => codeGraph.Nodes[d.SourceId]);

        return new Invocation(methods, calls);
    }

    public Invocation FindIncomingCallsRecursive(CodeGraph codeGraph, CodeElement method)
    {
        ArgumentNullException.ThrowIfNull(codeGraph);
        ArgumentNullException.ThrowIfNull(method);

        var processingQueue = new Queue<CodeElement>();
        processingQueue.Enqueue(method);

        var allCalls = new HashSet<Dependency>();
        var allMethods = new HashSet<CodeElement>();

        var callDependencies = GetCallDependencies(codeGraph);

        var processed = new HashSet<string>();
        while (processingQueue.Any())
        {
            var element = processingQueue.Dequeue();
            if (!processed.Add(element.Id))
            {
                continue;
            }

            var calls = callDependencies.Where(call => call.TargetId == element.Id).ToArray();
            allCalls.UnionWith(calls);

            var methods = calls.Select(d => codeGraph.Nodes[d.SourceId]).ToArray();
            allMethods.UnionWith(methods);

            foreach (var methodToExplore in methods)
            {
                processingQueue.Enqueue(codeGraph.Nodes[methodToExplore.Id]);
            }
        }

        return new Invocation(allMethods, allCalls);
    }

    /// <summary>
    ///     subclass -- inherits--> baseclass
    /// </summary>
    public SearchResult FindFullInheritanceTree(CodeGraph codeGraph, CodeElement type)
    {
        ArgumentNullException.ThrowIfNull(codeGraph);
        ArgumentNullException.ThrowIfNull(type);

        var types = new HashSet<CodeElement>();
        var relationships = new HashSet<Dependency>();
        var processingQueue = new Queue<CodeElement>();
        processingQueue.Enqueue(type);
        var processed = new HashSet<string>();

        var inheritsAndImplements = FindInheritsAndImplementsRelationships(codeGraph);
        while (processingQueue.Any())
        {
            var typeToAnalyze = processingQueue.Dequeue();
            if (!processed.Add(typeToAnalyze.Id))
            {
                // Since we evaluate both direction in one iteration, an already processed node is added again.
                continue;
            }

            // Case typeToAnalyze is subclass: typeToAnalyze implements X or inherits from Y
            var abstractionsOfAnalyzedType =
                typeToAnalyze.Dependencies.Where(d => d.Type is DependencyType.Implements or DependencyType.Inherits);
            foreach (var abstraction in abstractionsOfAnalyzedType)
            {
                var baseType = codeGraph.Nodes[abstraction.TargetId];
                types.Add(baseType);
                relationships.Add(abstraction);
                processingQueue.Enqueue(baseType);
            }

            // Case typeToAnalyze is base class: typeToAnalyze is implemented by X or Y inherits from it.
            var specializationsOfAnalyzedType
                = inheritsAndImplements.Where(d => typeToAnalyze.Id == d.TargetId);
            foreach (var specialization in specializationsOfAnalyzedType)
            {
                var specializedType = codeGraph.Nodes[specialization.SourceId];

                types.Add(specializedType);
                relationships.Add(specialization);
                processingQueue.Enqueue(specializedType);
            }
        }

        return new SearchResult(types, relationships);
    }

    /// <summary>
    ///     Returns all dependencies that link the given nodes (ids).
    /// </summary>
    public IEnumerable<Dependency> FindAllDependencies(HashSet<string> ids, CodeGraph? graph)
    {
        if (graph is null)
        {
            return [];
        }

        var dependencies = graph.Nodes.Values
            .SelectMany(n => n.Dependencies)
            .Where(d => ids.Contains(d.SourceId) && ids.Contains(d.TargetId))
            .ToList();

        return dependencies;
    }

    /// <summary>
    ///     x (source) -- derives from/overrides/implements --> y (target, search input)
    /// </summary>
    public SearchResult FindSpecializations(CodeGraph codeGraph, CodeElement element)
    {
        ArgumentNullException.ThrowIfNull(codeGraph);
        ArgumentNullException.ThrowIfNull(element);

        var dependencies = codeGraph.Nodes.Values.SelectMany(n => n.Dependencies)
            .Where(d => (d.Type == DependencyType.Overrides ||
                         d.Type == DependencyType.Implements) &&
                        d.TargetId == element.Id).ToList();
        var methods = dependencies.Select(m => codeGraph.Nodes[m.SourceId]).ToList();
        return new SearchResult(methods, dependencies);
    }

    /// <summary>
    ///     x (source, search input) -- derives from/overrides/implements --> y (target)
    /// </summary>
    public SearchResult FindAbstractions(CodeGraph codeGraph, CodeElement element)
    {
        ArgumentNullException.ThrowIfNull(codeGraph);
        ArgumentNullException.ThrowIfNull(element);

        var dependencies = element.Dependencies
            .Where(d => (d.Type == DependencyType.Overrides ||
                         d.Type == DependencyType.Implements) &&
                        d.SourceId == element.Id).ToList();
        var methods = dependencies.Select(m => codeGraph.Nodes[m.TargetId]).ToList();
        return new SearchResult(methods, dependencies);
    }


    public Invocation FindOutgoingCalls(CodeGraph codeGraph, CodeElement method)
    {
        ArgumentNullException.ThrowIfNull(codeGraph);
        ArgumentNullException.ThrowIfNull(method);

        var calls = method.Dependencies
            .Where(d => d.Type == DependencyType.Calls).ToList();
        var methods = calls.Select(m => codeGraph.Nodes[m.TargetId]).ToList();
        return new Invocation(methods, calls);
    }

    public SearchResult FindOutgoingDependencies(CodeGraph codeGraph, CodeElement element)
    {
        ArgumentNullException.ThrowIfNull(codeGraph);
        ArgumentNullException.ThrowIfNull(element);

        var dependencies = element.Dependencies;
        var targets = dependencies.Select(m => codeGraph.Nodes[m.TargetId]).ToList();
        return new SearchResult(targets, dependencies);
    }

    public SearchResult FindIncomingDependencies(CodeGraph codeGraph, CodeElement element)
    {
        ArgumentNullException.ThrowIfNull(codeGraph);
        ArgumentNullException.ThrowIfNull(element);

        var dependencies = codeGraph.Nodes.Values
            .SelectMany(node => node.Dependencies)
            .Where(d => d.TargetId == element.Id).ToList();

        var elements = dependencies.Select(d => codeGraph.Nodes[d.SourceId]);

        return new SearchResult(elements, dependencies);
    }

    private static List<Dependency> GetCallDependencies(CodeGraph codeGraph)
    {
        var callDependencies = codeGraph.Nodes.Values
            .SelectMany(node => node.Dependencies)
            .Where(d => d.Type == DependencyType.Calls)
            .ToList();
        return callDependencies;
    }

    private static HashSet<Dependency> FindInheritsAndImplementsRelationships(CodeGraph codeGraph)
    {
        var inheritsAndImplements = new HashSet<Dependency>();
        codeGraph.DfsHierarchy(Collect);
        return inheritsAndImplements;

        void Collect(CodeElement c)
        {
            if (c.ElementType is not (CodeElementType.Class or CodeElementType.Interface))
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

public record struct Relationship(IEnumerable<CodeElement> Types, IEnumerable<Dependency> Relationships);