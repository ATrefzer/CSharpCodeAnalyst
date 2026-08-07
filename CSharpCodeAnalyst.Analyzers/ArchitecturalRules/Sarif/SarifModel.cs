using System.Text.Json.Serialization;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Sarif;

/// <summary>
///     The subset of SARIF 2.1.0 this tool writes. Deliberately hand-written instead of taking the
///     Sarif.Sdk package: we fill maybe a tenth of the schema, the DTOs below are the whole cost, and
///     a new third-party dependency would have to be carried in ThirdPartyNotices and the README.
///     <para>
///         Every list is nullable and stays <c>null</c> when empty, so the emitted document has no
///         empty arrays - with one deliberate exception: <see cref="SarifRun.Results" /> is always
///         written, because in SARIF an absent result list means "nothing was analyzed" while an
///         empty one means "analyzed, found nothing". Only the latter is true of a clean run.
///         Property names are camel-cased by the serializer options in
///         <see cref="SarifFormatter" />; only "$schema" needs an explicit name.
///     </para>
/// </summary>
internal sealed class SarifLog
{
    [JsonPropertyName("$schema")]
    public string Schema { get; init; } = "https://json.schemastore.org/sarif-2.1.0.json";

    public string Version { get; init; } = "2.1.0";

    public List<SarifRun> Runs { get; init; } = [];
}

internal sealed class SarifRun
{
    public required SarifTool Tool { get; init; }

    /// <summary>Base directories result URIs are relative to, keyed by the id the URIs reference.</summary>
    public Dictionary<string, SarifArtifactLocation>? OriginalUriBaseIds { get; init; }

    /// <summary>Roslyn reports columns in UTF-16 code units, which is also what SARIF consumers assume.</summary>
    public string ColumnKind { get; init; } = "utf16CodeUnits";

    public List<SarifInvocation>? Invocations { get; init; }

    public List<SarifResult> Results { get; init; } = [];
}

internal sealed class SarifTool
{
    public required SarifToolComponent Driver { get; init; }
}

internal sealed class SarifToolComponent
{
    public required string Name { get; init; }

    /// <summary>Free-form version of the tool, exactly as the build stamped it.</summary>
    public string? Version { get; init; }

    /// <summary>
    ///     Only ever set when the version really is a semantic version - the spec requires SemVer 2.0.0
    ///     here, and a four-part .NET version like "0.1.0.0" is not one.
    /// </summary>
    public string? SemanticVersion { get; init; }

    public string? InformationUri { get; init; }

    public List<SarifReportingDescriptor>? Rules { get; init; }
}

/// <summary>Describes a kind of rule (DENY, MAXLINES, ...), not a single line of the rules file.</summary>
internal sealed class SarifReportingDescriptor
{
    public required string Id { get; init; }

    public SarifMessage? ShortDescription { get; init; }

    public SarifMessage? FullDescription { get; init; }

    public string? HelpUri { get; init; }

    public SarifReportingConfiguration? DefaultConfiguration { get; init; }
}

internal sealed class SarifReportingConfiguration
{
    public required string Level { get; init; }
}

internal sealed class SarifMessage
{
    public required string Text { get; init; }
}

internal sealed class SarifResult
{
    public required string RuleId { get; init; }

    public int RuleIndex { get; init; }

    public required string Level { get; init; }

    public required SarifMessage Message { get; init; }

    public List<SarifLocation>? Locations { get; init; }

    public List<SarifLocation>? RelatedLocations { get; init; }

    /// <summary>
    ///     Keeps an alert identifiable across runs. Without it every alert becomes a new one as soon as
    ///     a line moves, and dismissals in the consuming system are lost.
    /// </summary>
    public Dictionary<string, string>? PartialFingerprints { get; init; }

    public Dictionary<string, object>? Properties { get; init; }
}

internal sealed class SarifLocation
{
    public int? Id { get; init; }

    public SarifPhysicalLocation? PhysicalLocation { get; init; }

    public SarifMessage? Message { get; init; }
}

internal sealed class SarifPhysicalLocation
{
    public required SarifArtifactLocation ArtifactLocation { get; init; }

    public SarifRegion? Region { get; init; }
}

internal sealed class SarifArtifactLocation
{
    public required string Uri { get; init; }

    public string? UriBaseId { get; init; }
}

internal sealed class SarifRegion
{
    public int StartLine { get; init; }

    public int? StartColumn { get; init; }
}

internal sealed class SarifInvocation
{
    public bool ExecutionSuccessful { get; init; }

    /// <summary>
    ///     Problems with the run or its configuration - a rule that matches nothing, a parser failure.
    ///     These are not findings about the code and must not show up as results, or the number of
    ///     results would stop matching the exit code.
    /// </summary>
    public List<SarifNotification>? ToolConfigurationNotifications { get; init; }
}

internal sealed class SarifNotification
{
    public required string Level { get; init; }

    public required SarifMessage Message { get; init; }
}
