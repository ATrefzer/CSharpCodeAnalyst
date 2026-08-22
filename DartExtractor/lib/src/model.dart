/// The JSON contract between this tool and the C# side (DartGraphConverter).
///
/// [GraphElement.type] uses the literal names of CodeElementType and
/// [GraphRelationship.type] those of RelationshipType, so the C# side can parse
/// them with Enum.Parse. Keep both in sync when the enums change.
library;

class SourceLocation {
  SourceLocation(this.file, this.line, this.column);

  final String file;
  final int line;
  final int column;

  Map<String, Object?> toJson() => {'file': file, 'line': line, 'column': column};
}

class GraphElement {
  GraphElement({
    required this.id,
    required this.type,
    required this.name,
    required this.parentId,
    required this.isExternal,
    this.role,
    this.location,
  });

  final String id;
  final String type;
  final String name;

  /// `null` only for assemblies, which are the roots of the graph.
  final String? parentId;

  /// Everything outside the analyzed directory: the Dart/Flutter SDK and pub
  /// packages. Modelled the same way the C# parser models referenced assemblies.
  final bool isExternal;

  /// The literal name of a CodeElementType member's MemberRole, or `null` for
  /// anything that is not a method. Dart is the reason this travels on the wire
  /// at all: a named constructor `Foo.fromJson` arrives as a method called
  /// `fromJson`, so nothing on the C# side could tell it from an ordinary one.
  final String? role;

  final SourceLocation? location;

  Map<String, Object?> toJson() => {
        'id': id,
        'type': type,
        'name': name,
        if (parentId != null) 'parent': parentId,
        if (isExternal) 'external': true,
        if (role != null) 'role': role,
        if (location != null) 'location': location!.toJson(),
      };
}

/// Source metrics for one member, mirroring CSharpCodeAnalyst.CodeGraph.Metrics.MemberMetrics.
/// Emitted in a map keyed by element id, next to the graph rather than on the elements - the same
/// separation MetricStore makes on the C# side.
class MemberMetrics {
  MemberMetrics({
    required this.codeLines,
    required this.commentLines,
    required this.logicalLinesOfCode,
    required this.cyclomaticComplexity,
  });

  final int codeLines;
  final int commentLines;
  final int logicalLinesOfCode;
  final int cyclomaticComplexity;

  Map<String, Object?> toJson() => {
        'code': codeLines,
        'comment': commentLines,
        'logical': logicalLinesOfCode,
        'complexity': cyclomaticComplexity,
      };
}

class GraphRelationship {
  GraphRelationship(this.sourceId, this.targetId, this.type);

  final String sourceId;
  final String targetId;
  final String type;

  Map<String, Object?> toJson() => {'source': sourceId, 'target': targetId, 'type': type};
}
