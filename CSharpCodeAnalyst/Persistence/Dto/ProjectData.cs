using CSharpCodeAnalyst.CodeGraph.Declarations;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeGraph.Metrics;
using CSharpCodeAnalyst.Features.Gallery;

namespace CSharpCodeAnalyst.Persistence.Dto;

[Serializable]
public class ProjectData
{
    public List<SerializableChild> Children { get; set; } = [];

    public List<SerializableCodeElement> CodeElements { get; set; } = [];

    public List<SerializableRelationship> Relationships { get; set; } = [];

    public ProjectSettings Settings { get; set; } = new();

    /// <summary>
    ///     Analyzer persistent data. Key = Analyzer.Id, Value = JSON string from analyzer.
    /// </summary>
    public Dictionary<string, string> AnalyzerData { get; set; } = new();

    /// <summary>
    ///     Optional per-member source metrics (lines of code, cyclomatic complexity). Empty unless
    ///     metric collection was enabled during import.
    /// </summary>
    public List<SerializableMemberMetrics> MemberMetrics { get; set; } = [];

    /// <summary>
    ///     Which members implement a contract from outside the analyzed code, keyed by element id.
    ///     Empty for every graph producer except the C# parser. An older project file simply has none,
    ///     and those members show up as unreferenced again until the solution is parsed anew.
    /// </summary>
    public Dictionary<string, string> ExternalContracts { get; set; } = new();

    /// <summary>
    ///     The element ids of the types that raise change notifications (INotifyPropertyChanged anywhere
    ///     in the interface set). Complements <see cref="ExternalContracts" />: the member-level contract
    ///     cannot see a view model whose base class lives outside the analyzed code. An older project
    ///     file simply has none - those view models fall back to the member-based detection until the
    ///     solution is parsed anew.
    /// </summary>
    public List<string> NotifyingTypes { get; set; } = [];

    /// <summary>
    ///     Gallery is already serializable.
    /// </summary>
    public Gallery Gallery { get; set; } = new();

    public void SetGallery(Gallery gallery)
    {
        Gallery = gallery;
    }

    public Gallery GetGallery()
    {
        return Gallery;
    }

    public void SetMetrics(MetricStore store)
    {
        MemberMetrics = store.Metrics
            .Select(kvp => new SerializableMemberMetrics(kvp.Key, kvp.Value.CodeLines, kvp.Value.CommentLines,
                kvp.Value.LogicalLinesOfCode, kvp.Value.CyclomaticComplexity))
            .ToList();
    }

    public Dictionary<string, MemberMetrics> GetMetrics()
    {
        return MemberMetrics.ToDictionary(
            m => m.ElementId,
            m => new MemberMetrics
            {
                CodeLines = m.CodeLines,
                CommentLines = m.CommentLines,
                LogicalLinesOfCode = m.LogicalLinesOfCode,
                CyclomaticComplexity = m.CyclomaticComplexity
            });
    }

    public void SetExternalContracts(ExternalContractStore store)
    {
        ExternalContracts = store.Contracts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        NotifyingTypes = store.NotifyingTypes.ToList();
    }

    public IReadOnlyDictionary<string, string> GetExternalContracts()
    {
        return ExternalContracts;
    }

    public IReadOnlyCollection<string> GetNotifyingTypes()
    {
        return NotifyingTypes;
    }

    /// <summary>
    ///     Flatten the recursive structures.
    /// </summary>
    public void SetCodeGraph(CodeGraph.Graph.CodeGraph codeGraph)
    {
        CodeElements = codeGraph.Nodes.Values
            .Select(n =>
                new SerializableCodeElement(n.Id, n.Name, n.FullName, n.ElementType, n.SourceLocations, n.Attributes,
                    n.IsExternal, n.AccessLevel, n.IsGenerated))
            .ToList();

        // We iterate over children, so we expect to have a parent
        Children = codeGraph.Nodes.Values
            .SelectMany(element => element.Children)
            .Select(child => new SerializableChild(child.Id, child.Parent!.Id)).ToList();

        Relationships = codeGraph.Nodes.Values
            .SelectMany(element => element.Relationships)
            .Select(relationship => new SerializableRelationship(relationship.SourceId, relationship.TargetId,
                relationship.Type,
                (uint)relationship.Attributes,
                relationship.SourceLocations))
            .ToList();
    }

    public CodeGraph.Graph.CodeGraph GetCodeGraph()
    {
        var codeStructure = new CodeGraph.Graph.CodeGraph();

        // Pass one: Create elements
        foreach (var se in CodeElements)
        {
            var element = new CodeElement(se.Id, se.ElementType, se.Name, se.FullName, null!)
            {
                SourceLocations = se.SourceLocations,
                Attributes = se.Attributes,
                IsExternal = se.IsExternal,
                IsGenerated = se.IsGenerated,
                AccessLevel = se.AccessLevel
            };
            codeStructure.Nodes.Add(element.Id, element);
        }

        // Pass two: Create relationships and parent / child connections
        foreach (var sc in Children)
        {
            var child = codeStructure.Nodes[sc.ChildId];
            var parent = codeStructure.Nodes[sc.ParentId];
            child.Parent = parent;
            parent.Children.Add(child);
        }

        foreach (var sd in Relationships)
        {
            var source = codeStructure.Nodes[sd.SourceId];
            var relationship = new Relationship(sd.SourceId, sd.TargetId, sd.Type)
            {
                Attributes = (RelationshipAttribute)sd.Attributes,
                SourceLocations = sd.SourceLocations
            };
            source.Relationships.Add(relationship);
        }

        return codeStructure;
    }
}
