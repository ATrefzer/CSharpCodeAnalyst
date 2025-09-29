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

namespace CSharpCodeAnalyst.CommandLine
{
    internal class ConsoleValidationCommand : IPublisher
    {
        public void Publish<TMessage>(TMessage message) where TMessage : class
        {
            // Ignore, we get the result directly from the analyzer
        }

        public async Task<int> ValidateRules(string solutionPath, string rulesFilePath)
        {
            Console.WriteLine(Strings.Cmd_VerifyArchitecturalRules);
            Console.WriteLine(Strings.Cmd_SolutionFile, solutionPath);
            Console.WriteLine(Strings.Cmd_RulesFile, rulesFilePath);

            if (!File.Exists(solutionPath))
            {
                Console.WriteLine(Strings.Cmd_SolutionFileNotFound);
                return 1;
            }

            if (!File.Exists(rulesFilePath))
            {
                Console.WriteLine(Strings.Cmd_RulesFileNotFound);
                return 1;
            }

            // Initialize MSBuild
            Initializer.InitializeMsBuildLocator();

            var settings = LoadApplicationSettings();
            var graph = await ParseSolution(solutionPath, settings).ConfigureAwait(false);
            var violations = RunAnalysis(rulesFilePath, graph);

            Console.WriteLine(ViolationsFormatter.Format(graph, violations));

            var resultCode = violations.Count == 0 ? 0 : 1;
            Console.WriteLine(Strings.Cmd_AnalysisComplete, resultCode);
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
            var parser = new Parser(new ParserConfig(filter));
            var graph = await parser.ParseSolution(solutionPath).ConfigureAwait(false);

            var failures = parser.Diagnostics.FormatFailures();
            if (!string.IsNullOrEmpty(failures))
            {
                Console.WriteLine(Strings.Cmd_Failures);
                Console.WriteLine(failures);
            }

            var warnings = parser.Diagnostics.FormatWarnings();
            if (!string.IsNullOrEmpty(warnings))
            {
                Console.WriteLine(Strings.Cmd_Warnings);
                Console.WriteLine(warnings);
            }
          
            Console.WriteLine();
            return graph;
        }
    }
}