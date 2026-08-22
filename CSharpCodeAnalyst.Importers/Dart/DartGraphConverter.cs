using System.IO;
using System.Text.Json;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeGraph.Metrics;

namespace CSharpCodeAnalyst.Importers.Dart;

/// <summary>
///     Converts the JSON produced by the DartExtractor tool (DartExtractor/bin/extract.dart)
///     into a CodeGraph.
///     The Dart side already did the modelling - it emits the literal names of
///     <see cref="CodeElementType" /> and <see cref="RelationshipType" /> - so this converter only
///     rebuilds the object graph: parents before children (the JSON has no ordering guarantee),
///     full names from the parent chain, and relationships between known ids. Anything that does
///     not resolve is counted rather than thrown, so a newer extractor cannot break an import.
///     See Documentation/Dart/dart-import.md for the mapping itself.
/// </summary>
public class DartGraphConverter
{
    /// <summary>
    ///     Bumped whenever the JSON contract changes. Must match "format" in
    ///     DartExtractor/lib/src/graph_builder.dart. Extractor and application always ship together
    ///     (the deployment copies the tool out of the application directory), so a mismatch means a
    ///     broken installation rather than an old tool - hence the strict check.
    ///     2: added the per-member "metrics" map.
    ///     3: added "role" on an element - see MemberRole.
    /// </summary>
    public const int SupportedFormat = 3;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Dictionary<string, CodeElement> _created = new();
    private Dictionary<string, ElementDto> _dtosById = new();

    /// <summary>
    ///     Elements dropped because their type is unknown or their parent chain is broken.
    /// </summary>
    public int SkippedElements { get; private set; }

    /// <summary>
    ///     Relationships dropped because an endpoint or the relationship type is unknown.
    /// </summary>
    public int SkippedRelationships { get; private set; }

    /// <summary>
    ///     Per-member source metrics, filled by <see cref="ConvertFile" />. Empty when the extractor
    ///     found nothing to measure; only members with a body are measured.
    /// </summary>
    public MetricStore Metrics { get; } = new();

    public CodeGraph.Graph.CodeGraph ConvertFile(string jsonPath)
    {
        using var stream = File.OpenRead(jsonPath);
        var dto = JsonSerializer.Deserialize<GraphDto>(stream, SerializerOptions)
                  ?? throw new InvalidOperationException($"'{jsonPath}' is not a Dart graph.");
        return Convert(dto);
    }

    internal CodeGraph.Graph.CodeGraph Convert(GraphDto dto)
    {
        if (dto.Format != SupportedFormat)
        {
            throw new InvalidOperationException(
                $"Dart graph format {dto.Format} is not supported (expected {SupportedFormat}). The extractor and the application are out of sync.");
        }

        _dtosById = dto.Elements
            .GroupBy(e => e.Id)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var element in dto.Elements)
        {
            Materialize(element.Id, []);
        }

        foreach (var relationship in dto.Relationships)
        {
            AddRelationship(relationship);
        }

        foreach (var (elementId, metrics) in dto.Metrics ?? [])
        {
            // Metrics for an element that was skipped would never be looked up again.
            if (_created.ContainsKey(elementId))
            {
                Metrics.Add(elementId, new MemberMetrics
                {
                    CodeLines = metrics.Code,
                    CommentLines = metrics.Comment,
                    LogicalLinesOfCode = metrics.Logical,
                    CyclomaticComplexity = metrics.Complexity
                });
            }
        }

        return new CodeGraph.Graph.CodeGraph { Nodes = new Dictionary<string, CodeElement>(_created) };
    }

    /// <summary>
    ///     Creates the element and, recursively, everything above it. <paramref name="pending" />
    ///     only guards against a parent cycle in malformed input - the extractor cannot produce one.
    /// </summary>
    private CodeElement? Materialize(string id, HashSet<string> pending)
    {
        if (_created.TryGetValue(id, out var existing))
        {
            return existing;
        }

        if (!_dtosById.TryGetValue(id, out var dto) || !pending.Add(id))
        {
            return null;
        }

        try
        {
            if (!Enum.TryParse<CodeElementType>(dto.Type, out var elementType))
            {
                SkippedElements++;
                return null;
            }

            CodeElement? parent = null;
            if (dto.Parent is not null)
            {
                parent = Materialize(dto.Parent, pending);
                if (parent is null)
                {
                    SkippedElements++;
                    return null;
                }
            }

            var fullName = parent is null ? dto.Name : parent.FullName + "." + dto.Name;
            var element = new CodeElement(dto.Id, elementType, dto.Name, fullName, parent)
            {
                IsExternal = dto.External,

                // The extractor decides this - a named constructor "Foo.fromJson" is a method called
                // "fromJson", so nothing here could work it out. An unreadable value stays Unknown
                // rather than being guessed, exactly like an unresolvable element type.
                MemberRole = dto.Role is not null && Enum.TryParse<MemberRole>(dto.Role, out var role)
                    ? role
                    : MemberRole.Unknown
            };

            if (dto.Location is { } location)
            {
                element.SourceLocations.Add(new SourceLocation(ToSystemPath(location.File), location.Line, location.Column));
            }

            parent?.Children.Add(element);
            _created[dto.Id] = element;
            return element;
        }
        finally
        {
            pending.Remove(id);
        }
    }

    private void AddRelationship(RelationshipDto dto)
    {
        if (!Enum.TryParse<RelationshipType>(dto.Type, out var relationshipType) ||
            !_created.TryGetValue(dto.Source, out var source) ||
            !_created.ContainsKey(dto.Target))
        {
            SkippedRelationships++;
            return;
        }

        // Relationship compares by (source, target, type), so the HashSet deduplicates - the
        // extractor already does, but a duplicate must never turn into a parallel edge.
        source.Relationships.Add(new Relationship(dto.Source, dto.Target, relationshipType));
    }

    private static string ToSystemPath(string path)
    {
        return path.Replace('/', Path.DirectorySeparatorChar);
    }

    internal sealed record LocationDto(string File, int Line, int Column);

    internal sealed record ElementDto(string Id, string Type, string Name, string? Parent, bool External, LocationDto? Location,
        string? Role);

    internal sealed record RelationshipDto(string Source, string Target, string Type);

    internal sealed record MetricsDto(int Code, int Comment, int Logical, int Complexity);

    internal sealed record GraphDto(int Format, string ProjectName, List<ElementDto> Elements, List<RelationshipDto> Relationships,
        Dictionary<string, MetricsDto>? Metrics);
}
