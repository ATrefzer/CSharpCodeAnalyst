using CSharpCodeAnalyst.CodeGraph.Contracts;
using System.Diagnostics;
using System.IO;
using System.Text;
using CSharpCodeAnalyst.Configuration;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules;
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
        var (graph, metricStore) = await ParseSolution(solutionFile, settings).ConfigureAwait(false);
        var analysisResult = RunAnalysis(rulesFile, graph, metricStore);

        // Write output
        var result = ViolationsFormatter.Format(graph, analysisResult);
        var outFile = arguments.GetValueOrDefault("out");
        if (!string.IsNullOrEmpty(outFile))
        {
            await File.WriteAllTextAsync(outFile, result, Encoding.UTF8);
        }

        Trace.WriteLine(result);

        var resultCode = analysisResult.Violations.Count == 0 ? 0 : 1;
        Trace.TraceInformation(Strings.Cmd_AnalysisComplete, resultCode);
        return resultCode;
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

    private static async Task<(CodeGraph.Graph.CodeGraph Graph, MetricStore MetricStore)> ParseSolution(string solutionPath, AppSettings settings)
    {
        var filter = new ProjectExclusionRegExCollection();
        filter.Initialize(settings.DefaultProjectExcludeFilter);
        var parser = new Parser(new ParserConfig(filter, settings.IncludeExternalCode, settings.SplitPropertyAccessors));
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
        return (parseResult.CodeGraph, parseResult.Metrics);
    }

    public bool CanExecute()
    {
        // Required
        return arguments.ContainsKey("validate")
               && arguments.ContainsKey("rules")
               && arguments.ContainsKey("sln");
    }
}