using CSharpCodeAnalyst.CodeGraph.Contracts;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using CSharpCodeAnalyst.Configuration;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules.Sarif;
using CSharpCodeAnalyst.AnalyzerSdk.Contracts;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.Contracts;
using CSharpCodeAnalyst.Shared.Messages;
using CSharpCodeAnalyst.Shared.Notifications;
using Microsoft.Extensions.Configuration;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeGraph.Metrics;
using CSharpCodeAnalyst.CodeParser.Parser;
using CSharpCodeAnalyst.CodeParser.Parser.Config;

namespace CSharpCodeAnalyst.CommandLine;

internal class ConsoleValidationCommand(Dictionary<string, string> arguments) : IPublisher
{
    public void Publish<TMessage>(TMessage message) where TMessage : class
    {
        // Ignore, we get the result directly from the analyzer
    }

    public async Task<int> Execute()
    {
        var rulesFile = arguments["rules"];
        var solutionFile = arguments["sln"];

        Trace.TraceInformation(Strings.Cmd_VerifyArchitecturalRules);
        Trace.TraceInformation(Strings.Cmd_SolutionFile, solutionFile);
        Trace.TraceInformation(Strings.Cmd_RulesFile, rulesFile);

        if (!File.Exists(solutionFile))
        {
            Trace.TraceError(Strings.Cmd_SolutionFileNotFound);
            return 2;
        }

        if (!File.Exists(rulesFile))
        {
            Trace.TraceError(Strings.Cmd_RulesFileNotFound);
            return 2;
        }

        // Initialize MSBuild
        Initializer.InitializeMsBuildLocator();

        // Parse solution and do analysis
        var settings = LoadAppSettings();
        var (graph, metricStore, hasParserFailures) = await ParseSolution(solutionFile, settings).ConfigureAwait(false);
        if (hasParserFailures)
        {
            // A parser failure (e.g. a referenced project's build output missing for a WPF
            // markup-compile pass) is typically narrow: measured on this repo, 4 out of 9652
            // graph elements were missing for two unresolvable XAML tags - everything else,
            // including every .cs file, still parsed. Not worth discarding the whole run over;
            // just make sure it is impossible to miss in the output.
            Trace.TraceWarning(Strings.Cmd_ParserFailuresWarning);
        }

        var analysisResult = RunAnalysis(rulesFile, graph, metricStore);

        // Write output
        var result = ViolationsFormatter.Format(graph, analysisResult);
        var outFile = arguments.GetValueOrDefault("out");
        if (!string.IsNullOrEmpty(outFile))
        {
            CommandLineProcessor.EnsureDirectoryExists(outFile);
            await File.WriteAllTextAsync(outFile, result, Encoding.UTF8);
        }

        Trace.WriteLine(result);

        await WriteSarifAsync(graph, analysisResult, solutionFile, rulesFile, hasParserFailures).ConfigureAwait(false);

        var resultCode = analysisResult.Violations.Count == 0 ? 0 : 1;
        Trace.TraceInformation(Strings.Cmd_AnalysisComplete, resultCode);
        return resultCode;
    }

    /// <summary>
    ///     SARIF is written to its own file rather than replacing the text of -out: a CI job wants both
    ///     - the readable text in its log and the SARIF for the code scanning upload - and an existing
    ///     -out script must not change its meaning.
    /// </summary>
    private async Task WriteSarifAsync(
        CodeGraph.Graph.CodeGraph graph,
        RuleAnalysisResult analysisResult,
        string solutionFile,
        string rulesFile,
        bool hasParserFailures)
    {
        var sarifFile = arguments.GetValueOrDefault("sarif");
        if (string.IsNullOrEmpty(sarifFile))
        {
            return;
        }

        // The solution is not necessarily at the repository root (it often sits in src\), and the
        // consumer needs paths relative to the root - hence the override.
        var sourceRoot = arguments.GetValueOrDefault("source-root");
        if (string.IsNullOrEmpty(sourceRoot))
        {
            sourceRoot = Path.GetDirectoryName(Path.GetFullPath(solutionFile));
        }

        var context = new SarifContext
        {
            SourceRoot = sourceRoot,
            RulesFile = rulesFile,
            ToolVersion = GetToolVersion(),
            RunNotifications = hasParserFailures ? [Strings.Cmd_ParserFailuresWarning] : []
        };

        var sarif = SarifFormatter.Format(graph, analysisResult, context);
        CommandLineProcessor.EnsureDirectoryExists(sarifFile);
        await File.WriteAllTextAsync(sarifFile, sarif, new UTF8Encoding(false));

        Trace.TraceInformation(Strings.Cmd_SarifWritten, sarifFile);
    }

    /// <summary>
    ///     The informational version, kept whole - it carries the source revision after a '+', which is
    ///     what pins a report to the exact build that produced it. Trimming it down to something that
    ///     looks like a semantic version is the SARIF layer's job, and only it knows when that is
    ///     possible at all.
    /// </summary>
    private static string GetToolVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        return string.IsNullOrEmpty(informational)
            ? assembly.GetName().Version?.ToString() ?? "0.0.0"
            : informational;
    }

    private static AppSettings LoadAppSettings()
    {
        // Resolve next to the executable, not the process's current working directory: headless
        // -validate is typically invoked from CI scripts (e.g. Start-Process without an explicit
        // -WorkingDirectory) whose CWD has no relation to where the tool was extracted/installed.
        // Missing the file is not an error here - fall back to AppSettings' own defaults, the same
        // as an interactive first run with no saved settings yet.
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", true, true);

        IConfiguration configuration = builder.Build();
        var settings = configuration.GetSection("ApplicationSettings").Get<AppSettings>();
        settings ??= new AppSettings();
        return settings;
    }

    private static RuleAnalysisResult RunAnalysis(string rulesFilePath, CodeGraph.Graph.CodeGraph graph, MetricStore metricStore)
    {
        var messaging = new MessageBus();
        var messageBox = new ConsoleUserNotification();
        var analyzer = new Analyzer(messaging, messageBox, metricStore);

        return analyzer.Analyze(graph, rulesFilePath, metricStore);
    }

    private static async Task<(CodeGraph.Graph.CodeGraph Graph, MetricStore MetricStore, bool HasFailures)> ParseSolution(string solutionPath, AppSettings settings)
    {
        var filter = new ProjectExclusionRegExCollection();
        filter.Initialize(settings.DefaultProjectExcludeFilter);
        var parser = new Parser(new ParserConfig(filter, settings.IncludeExternalCode));
        var parseResult = await parser.ParseAsync(solutionPath).ConfigureAwait(false);

        var failures = parser.Diagnostics.FormatFailures();
        if (!string.IsNullOrEmpty(failures))
        {
            Trace.TraceError(Strings.Cmd_Failures);
            Trace.TraceError(failures);
        }

        var warnings = parser.Diagnostics.FormatWarnings();
        if (!string.IsNullOrEmpty(warnings))
        {
            Trace.TraceWarning(Strings.Cmd_Warnings);
            Trace.TraceWarning(warnings);
        }

        Trace.WriteLine("\n");
        return (parseResult.CodeGraph, parseResult.Metrics, parser.Diagnostics.Failures.Count > 0);
    }

    public bool CanExecute()
    {
        // Required
        return arguments.ContainsKey("validate")
               && arguments.ContainsKey("rules")
               && arguments.ContainsKey("sln");
    }
}