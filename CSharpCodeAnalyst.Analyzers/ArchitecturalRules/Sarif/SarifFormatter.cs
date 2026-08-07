using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Encodings.Web;
using System.Text.Json;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Rules;
using CSharpCodeAnalyst.CodeGraph.Graph;

namespace CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Sarif;

/// <summary>
///     Writes the result of a rule validation as a SARIF 2.1.0 log, the interchange format CI systems
///     (GitHub code scanning, Azure DevOps, SonarQube) read to turn findings into annotations.
///     <para>
///         The counterpart of <see cref="ViolationsFormatter" />, which stays the human-readable
///         output. Both are produced from the same <see cref="RuleAnalysisResult" />; neither replaces
///         the other.
///     </para>
/// </summary>
public static class SarifFormatter
{
    /// <summary>
    ///     Name of the fingerprint that identifies a finding across runs. Versioned, because changing
    ///     how the fingerprint is computed makes every existing alert a new one - a new version is the
    ///     signal to a consumer that the old ones cannot be matched, rather than a silent reshuffle.
    /// </summary>
    public const string FingerprintKey = "codeAnalystFingerprint/v1";

    private const string ToolName = "CSharpCodeAnalyst";
    private const string ToolInformationUri = "https://github.com/ATrefzer/CSharpCodeAnalyst";

    private const string HelpUri =
        "https://github.com/ATrefzer/CSharpCodeAnalyst/blob/main/Documentation/architectural-rules.md";

    /// <summary>A cycle can have hundreds of participants; annotating every one of them is noise.</summary>
    private const int MaxCycleLocations = 25;

    /// <summary>MAJOR.MINOR.PATCH with an optional pre-release part - the release half of SemVer 2.0.0.</summary>
    private static readonly Regex SemanticVersionRegex = new(
        @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$",
        RegexOptions.Compiled);

    /// <summary>
    ///     Rule descriptions are deliberately not taken from Strings.resx. This is machine output read
    ///     by a CI system, and it must not change with the locale of the machine that happens to run
    ///     the validation - a translated description would silently change the alert text.
    /// </summary>
    private static readonly Dictionary<string, (string Short, string Full)> RuleDescriptions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["DENY"] = ("Forbidden dependency",
                "A dependency exists that a DENY rule forbids. DENY is the only rule that also covers dependencies to external code, so it is the one that can forbid a specific framework or package."),
            ["RESTRICT"] = ("Dependency outside the permitted targets",
                "A RESTRICT rule limits what its source may depend on. This dependency leaves the source and does not end in any permitted target. RESTRICT rules with overlapping sources widen each other, and dependencies to external code are always permitted."),
            ["ISOLATE"] = ("Dependency out of an isolated element",
                "An ISOLATE rule allows only incoming dependencies: its element must not depend on anything outside itself, which makes it a leaf of the dependency graph. Dependencies to external code are always permitted, and an ALLOW rule can except individual ones."),
            ["NOCYCLES"] = ("Dependency cycle",
                "A NOCYCLES rule requires its element and everything below it to be free of dependency cycles, including cycles that only exist between namespaces. Mutual recursion between the members of a single type is a code pattern, not an architecture violation, and does not count."),
            ["MAXCYCLICITY"] = ("System cyclicity above the threshold",
                "The share of internal types that sit inside a dependency cycle exceeds the configured maximum. Measured on the plain type dependency graph: a cycle that only exists between namespaces is not counted here, NOCYCLES is the rule that sees those."),
            ["MAXLINES"] = ("Element longer than the threshold",
                "A code element has more code lines than the rule permits.")
        };

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,

        // The default encoder escapes non-ASCII and characters like '+' in the file URIs. A SARIF log
        // is UTF-8 and read by machines, so keep it readable and diffable instead.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Format(CodeGraph.Graph.CodeGraph graph, RuleAnalysisResult result, SarifContext context)
    {
        var paths = new SarifPathMapper(context.SourceRoot);
        var rulesFile = paths.Map(context.RulesFile);

        // First appearance decides the index, so the descriptor table follows the results.
        var ruleIndices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var ruleIds = new List<string>();

        var results = new List<SarifResult>();
        foreach (var violation in result.Violations)
        {
            foreach (var sarifResult in CreateResults(graph, violation, paths, rulesFile))
            {
                if (!ruleIndices.TryGetValue(sarifResult.RuleId, out var index))
                {
                    index = ruleIds.Count;
                    ruleIndices[sarifResult.RuleId] = index;
                    ruleIds.Add(sarifResult.RuleId);
                }

                results.Add(WithRuleIndex(sarifResult, index));
            }
        }

        var log = new SarifLog
        {
            Runs =
            [
                new SarifRun
                {
                    Tool = new SarifTool
                    {
                        Driver = new SarifToolComponent
                        {
                            Name = ToolName,
                            Version = context.ToolVersion,
                            SemanticVersion = ToSemanticVersion(context.ToolVersion),
                            InformationUri = ToolInformationUri,
                            Rules = ruleIds.Count == 0 ? null : ruleIds.Select(CreateDescriptor).ToList()
                        }
                    },
                    OriginalUriBaseIds = CreateUriBaseIds(paths),
                    Invocations = [CreateInvocation(result, context)],
                    Results = results
                }
            ]
        };

        return JsonSerializer.Serialize(log, Options);
    }

    /// <summary>
    ///     The version as a SARIF <c>semanticVersion</c>, or <c>null</c> when it is not one.
    ///     <para>
    ///         The spec requires SemVer 2.0.0 in that field, and a .NET version is not necessarily a
    ///         semantic version: a build without an explicit <c>-p:Version</c> stamps the four-part
    ///         "0.1.0.0". Writing that anyway would produce a log that does not validate; truncating it
    ///         to three parts would be worse still, because two different builds would then claim the
    ///         same version. So the field is simply omitted, and the free-form <c>version</c> - which
    ///         keeps the build metadata, including the commit the report came from - carries the truth.
    ///     </para>
    /// </summary>
    private static string? ToSemanticVersion(string version)
    {
        // Build metadata after '+' is legal in SemVer but noise here; the release part is what a
        // consumer compares.
        var release = version.Split('+')[0];
        return SemanticVersionRegex.IsMatch(release) ? release : null;
    }

    private static Dictionary<string, SarifArtifactLocation>? CreateUriBaseIds(SarifPathMapper paths)
    {
        var rootUri = paths.RootUri;
        return rootUri is null
            ? null
            : new Dictionary<string, SarifArtifactLocation>
            {
                [SarifPathMapper.SourceRootId] = new() { Uri = rootUri }
            };
    }

    /// <summary>
    ///     A dead rule and a parser failure are problems of this run, not findings about the code, and
    ///     belong into the invocation. Keeping them out of the results is what lets a consumer treat
    ///     "results is empty" as "the architecture is clean" - the same statement the exit code makes.
    /// </summary>
    private static SarifInvocation CreateInvocation(RuleAnalysisResult result, SarifContext context)
    {
        var notifications = result.Warnings
            .Concat(context.RunNotifications)
            .Select(text => new SarifNotification
            {
                Level = "warning",
                Message = new SarifMessage { Text = text }
            })
            .ToList();

        return new SarifInvocation
        {
            ExecutionSuccessful = true,
            ToolConfigurationNotifications = notifications.Count == 0 ? null : notifications
        };
    }

    private static SarifReportingDescriptor CreateDescriptor(string ruleId)
    {
        var hasDescription = RuleDescriptions.TryGetValue(ruleId, out var description);

        return new SarifReportingDescriptor
        {
            Id = ruleId,
            ShortDescription = hasDescription ? new SarifMessage { Text = description.Short } : null,
            FullDescription = hasDescription ? new SarifMessage { Text = description.Full } : null,
            HelpUri = HelpUri,

            // Every violation fails the validation (exit code 1), so nothing here is a mere warning.
            DefaultConfiguration = new SarifReportingConfiguration { Level = "error" }
        };
    }

    /// <summary>
    ///     One violation can cover many places in the code. Dependency and code element metric rules are
    ///     therefore split into one result per relationship / element, so that a consumer annotates each
    ///     offending line instead of putting one collective note somewhere.
    /// </summary>
    private static IEnumerable<SarifResult> CreateResults(
        CodeGraph.Graph.CodeGraph graph,
        Violation violation,
        SarifPathMapper paths,
        SarifArtifactLocation? rulesFile)
    {
        var rule = violation.Rule;
        var ruleLocation = CreateRuleLocation(rulesFile, rule.LineNumber);

        if (violation.ViolatingRelationships.Count > 0)
        {
            return CreateDependencyResults(graph, violation, paths, ruleLocation);
        }

        if (violation.CycleElements.Count > 0)
        {
            return [CreateCycleResult(violation, paths, ruleLocation)];
        }

        if (violation.ViolatingElements.Count > 0)
        {
            return CreateElementMetricResults(violation, paths, ruleLocation);
        }

        // System metric rules - and any future violation shape that carries no place in the code. The
        // rule line is the only location there is, and a result without any location is dropped by
        // some consumers, so it becomes the primary one instead of a related one.
        return
        [
            CreateResult(
                rule,
                violation.Description,
                null,
                ruleLocation,
                Fingerprint(rule.DisplayName, rule.RuleText),
                MetricProperties(violation))
        ];
    }

    private static List<SarifResult> CreateDependencyResults(
        CodeGraph.Graph.CodeGraph graph,
        Violation violation,
        SarifPathMapper paths,
        SarifLocation? ruleLocation)
    {
        var rule = violation.Rule;
        var results = new List<SarifResult>();

        foreach (var relationship in SortForOutput(violation.ViolatingRelationships, graph))
        {
            if (!graph.Nodes.TryGetValue(relationship.SourceId, out var source) ||
                !graph.Nodes.TryGetValue(relationship.TargetId, out var target))
            {
                // Dangling edge - the text formatter reports it as an invalid relationship. There is
                // nothing to annotate in the code, so it does not become a SARIF result.
                continue;
            }

            var locations = MapLocations(relationship.SourceLocations, paths);
            if (locations.Count == 0)
            {
                // A relationship without a location of its own (for example one derived rather than
                // read from a syntax node) still has to be reportable - fall back to the source
                // element's declaration.
                locations = MapLocations(source.SourceLocations, paths);
            }

            var message =
                $"{Describe(rule)}: {source.FullName} -> {target.FullName} ({relationship.Type})";

            results.Add(CreateResult(
                rule,
                message,
                locations,
                ruleLocation,
                Fingerprint(rule.DisplayName, source.FullName, target.FullName, relationship.Type.ToString()),
                new Dictionary<string, object>
                {
                    ["ruleText"] = rule.RuleText,
                    ["sourceElement"] = source.FullName,
                    ["targetElement"] = target.FullName,
                    ["relationshipType"] = relationship.Type.ToString()
                }));
        }

        return results;
    }

    /// <summary>
    ///     Orders by the names of the two ends, so that two runs over an unchanged code base produce
    ///     byte-identical files. The graph is built in parallel and has no order of its own.
    /// </summary>
    private static IEnumerable<Relationship> SortForOutput(
        IEnumerable<Relationship> relationships,
        CodeGraph.Graph.CodeGraph graph)
    {
        string Name(string id)
        {
            return graph.Nodes.TryGetValue(id, out var element) ? element.FullName : id;
        }

        return relationships
            .OrderBy(r => Name(r.SourceId), StringComparer.Ordinal)
            .ThenBy(r => Name(r.TargetId), StringComparer.Ordinal)
            .ThenBy(r => r.Type);
    }

    /// <summary>
    ///     One result per cycle group: a cycle is a property of the group, and splitting it into its
    ///     edges would report the same finding as many unrelated alerts. The participants become the
    ///     locations so the cycle is at least visible in the diff of any of them.
    /// </summary>
    private static SarifResult CreateCycleResult(Violation violation, SarifPathMapper paths, SarifLocation? ruleLocation)
    {
        var participants = violation.CycleElements
            .OrderBy(e => e.FullName, StringComparer.Ordinal)
            .ToList();

        var locations = participants
            .SelectMany(e => e.SourceLocations.Take(1))
            .Take(MaxCycleLocations)
            .ToList();

        return CreateResult(
            violation.Rule,
            violation.Description,
            MapLocations(locations, paths),
            ruleLocation,
            Fingerprint(violation.Rule.DisplayName, string.Join(",", participants.Select(e => e.FullName))),
            new Dictionary<string, object>
            {
                ["ruleText"] = violation.Rule.RuleText,
                ["cycleName"] = violation.CycleName ?? string.Empty,
                ["participantCount"] = participants.Count,
                ["participants"] = participants.Select(e => e.FullName).ToList()
            });
    }

    private static List<SarifResult> CreateElementMetricResults(
        Violation violation,
        SarifPathMapper paths,
        SarifLocation? ruleLocation)
    {
        var rule = violation.Rule;
        var metricRule = rule as MetricRule;
        var results = new List<SarifResult>();

        foreach (var (element, value) in violation.ViolatingElements)
        {
            var actual = metricRule is null ? value.ToString("0.##", CultureInfo.InvariantCulture) : metricRule.FormatValue(value);
            var threshold = metricRule is null ? string.Empty : metricRule.FormatValue(metricRule.Threshold);

            var message = $"{Describe(rule)}: {element.FullName} is {actual}, the limit is {threshold}.";

            results.Add(CreateResult(
                rule,
                message,
                MapLocations(element.SourceLocations, paths),
                ruleLocation,
                Fingerprint(rule.DisplayName, element.FullName),
                new Dictionary<string, object>
                {
                    ["ruleText"] = rule.RuleText,
                    ["element"] = element.FullName,
                    ["value"] = value,
                    ["threshold"] = metricRule?.Threshold ?? 0d
                }));
        }

        return results;
    }

    private static Dictionary<string, object> MetricProperties(Violation violation)
    {
        var properties = new Dictionary<string, object> { ["ruleText"] = violation.Rule.RuleText };

        if (violation.MetricValue.HasValue)
        {
            properties["value"] = violation.MetricValue.Value;
        }

        if (violation.Rule is MetricRule metricRule)
        {
            properties["threshold"] = metricRule.Threshold;
        }

        return properties;
    }

    private static SarifResult CreateResult(
        RuleBase rule,
        string message,
        List<SarifLocation>? locations,
        SarifLocation? ruleLocation,
        string fingerprint,
        Dictionary<string, object> properties)
    {
        var hasCodeLocation = locations is { Count: > 0 };

        return new SarifResult
        {
            RuleId = rule.DisplayName,
            Level = "error",
            Message = new SarifMessage { Text = message },

            // Without a location in the code the rule line is the location, otherwise it is what the
            // finding relates to - a click from the alert to the rule that produced it.
            Locations = hasCodeLocation ? locations : Wrap(AsPrimary(ruleLocation)),
            RelatedLocations = hasCodeLocation ? Wrap(ruleLocation) : null,
            PartialFingerprints = new Dictionary<string, string> { [FingerprintKey] = fingerprint },
            Properties = properties
        };
    }

    private static List<SarifLocation>? Wrap(SarifLocation? location)
    {
        return location is null ? null : [location];
    }

    /// <summary>
    ///     Drops what only makes sense on a related location: the reference id nothing points at, and
    ///     the "Rule defined here" caption, which reads wrong on the place the finding itself is at.
    /// </summary>
    private static SarifLocation? AsPrimary(SarifLocation? location)
    {
        return location is null ? null : new SarifLocation { PhysicalLocation = location.PhysicalLocation };
    }

    private static SarifResult WithRuleIndex(SarifResult result, int index)
    {
        return new SarifResult
        {
            RuleId = result.RuleId,
            RuleIndex = index,
            Level = result.Level,
            Message = result.Message,
            Locations = result.Locations,
            RelatedLocations = result.RelatedLocations,
            PartialFingerprints = result.PartialFingerprints,
            Properties = result.Properties
        };
    }

    private static SarifLocation? CreateRuleLocation(SarifArtifactLocation? rulesFile, int lineNumber)
    {
        if (rulesFile is null || lineNumber <= 0)
        {
            return null;
        }

        return new SarifLocation
        {
            Id = 1,
            Message = new SarifMessage { Text = "Rule defined here" },
            PhysicalLocation = new SarifPhysicalLocation
            {
                ArtifactLocation = rulesFile,
                Region = new SarifRegion { StartLine = lineNumber }
            }
        };
    }

    private static List<SarifLocation> MapLocations(IEnumerable<SourceLocation> locations, SarifPathMapper paths)
    {
        var mapped = new List<SarifLocation>();

        foreach (var location in locations)
        {
            var artifact = paths.Map(location.File);
            if (artifact is null)
            {
                continue;
            }

            mapped.Add(new SarifLocation
            {
                PhysicalLocation = new SarifPhysicalLocation
                {
                    ArtifactLocation = artifact,

                    // Both are already 1-based in the graph, which is what SARIF expects. A missing
                    // line would be an invalid region, so such a location is reduced to the file.
                    Region = location.Line > 0
                        ? new SarifRegion
                        {
                            StartLine = location.Line,
                            StartColumn = location.Column > 0 ? location.Column : null
                        }
                        : null
                }
            });
        }

        return mapped;
    }

    private static string Describe(RuleBase rule)
    {
        return string.IsNullOrWhiteSpace(rule.RuleText) ? rule.DisplayName : rule.RuleText;
    }

    /// <summary>
    ///     Identity of a finding, built from what the finding says about the architecture - never from
    ///     a file or a line. Moving a class within its file, or the whole file, must not turn an
    ///     acknowledged alert into a new one.
    /// </summary>
    private static string Fingerprint(params string[] parts)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", parts)));

        // Half of the digest is far more than enough to keep findings of one run apart, and keeps the
        // log readable.
        return Convert.ToHexStringLower(bytes.AsSpan(0, 16));
    }
}
