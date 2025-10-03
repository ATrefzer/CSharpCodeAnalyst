using System.Diagnostics;
using System.IO;
using System.Text;
using CodeParser.Parser;
using CodeParser.Parser.Config;
using Contracts.Graph;
using CSharpCodeAnalyst.Analyzers.ArchitecturalRules;
using CSharpCodeAnalyst.Common;
using CSharpCodeAnalyst.Configuration;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.Contracts;
using Microsoft.Extensions.Configuration;

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
        var settings = LoadApplicationSettings();
        var graph = await ParseSolution(solutionFile, settings).ConfigureAwait(false);
        var violations = RunAnalysis(rulesFile, graph);

        // Write output
        var result = ViolationsFormatter.Format(graph, violations);
        var outFile = arguments.GetValueOrDefault("out");
        if (!string.IsNullOrEmpty(outFile))
        {
            await File.WriteAllTextAsync(outFile, result, Encoding.UTF8);
        }

        Trace.WriteLine(result);

        var resultCode = violations.Count == 0 ? 0 : 1;
        Trace.TraceInformation(Strings.Cmd_AnalysisComplete, resultCode);
        return resultCode;
    }

    private static ApplicationSettings LoadApplicationSettings()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", false, true);

        IConfiguration configuration = builder.Build();
        var settings = configuration.GetSection("ApplicationSettings").Get<ApplicationSettings>();
        settings ??= new ApplicationSettings();
        return settings;
    }

    private static List<Violation> RunAnalysis(string rulesFilePath, CodeGraph graph)
    {
        var messaging = new MessageBus();
        var messageBox = new ConsoleMessageBox();
        var analyzer = new Analyzer(messaging, messageBox);

        var violations = analyzer.Analyze(graph, rulesFilePath);
        return violations;
    }

    private static async Task<CodeGraph> ParseSolution(string solutionPath, ApplicationSettings settings)
    {
        var filter = new ProjectExclusionRegExCollection();
        filter.Initialize(settings.DefaultProjectExcludeFilter, ";");
        var parser = new Parser(new ParserConfig(filter, settings.IncludeExternalCode));
        var graph = await parser.ParseSolution(solutionPath).ConfigureAwait(false);

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
        return graph;
    }

    public bool CanExecute()
    {
        // Required
        return arguments.ContainsKey("validate")
               && arguments.ContainsKey("rules")
               && arguments.ContainsKey("sln");
    }
}