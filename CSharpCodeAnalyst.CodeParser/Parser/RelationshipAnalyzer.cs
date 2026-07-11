using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeParser.Parser.Config;
using Microsoft.CodeAnalysis;

namespace CSharpCodeAnalyst.CodeParser.Parser;

/// <summary>
///     Phase 2/2 of the parser: Analyzing relationships between code elements.
///     This class only orchestrates the phase (parallel loop, progress, global statements); the actual
///     work is split by responsibility:
///     <list type="bullet">
///         <item><see cref="DeclarationAnalyzer" /> - what an element depends on through its declaration.</item>
///         <item><see cref="SyntaxNodeAnalyzer" /> - what a body references (fed by the syntax walkers).</item>
///         <item><see cref="RelationshipBuilder" /> - symbol resolution and the actual graph writes.</item>
///     </list>
///     All three are created fresh per run, so no state leaks between parses.
/// </summary>
public class RelationshipAnalyzer
{
    private readonly ParserConfig _config;
    private readonly IProgress<string>? _progress;
    private long _lastProgress;
    private int _processedCodeElements;

    public RelationshipAnalyzer(IProgress<string>? progress, ParserConfig config)
    {
        _progress = progress;
        _config = config;
    }

    /// <summary>
    ///     Builds all relationships (phase 2). The code graph is updated, the artifacts are read only.
    ///     Pass <paramref name="maxDegreeOfParallelism" /> = 1 for a deterministic single-threaded run
    ///     (useful when debugging); the default (-1) lets the scheduler use all available cores.
    /// </summary>
    public Task AnalyzeRelationships(Solution solution, CodeGraph.Graph.CodeGraph codeGraph, Artifacts artifacts,
        int maxDegreeOfParallelism = -1)
    {
        ArgumentNullException.ThrowIfNull(solution, nameof(solution));
        ArgumentNullException.ThrowIfNull(codeGraph, nameof(codeGraph));
        ArgumentNullException.ThrowIfNull(artifacts, nameof(artifacts));

        var builder = new RelationshipBuilder(codeGraph, artifacts, _config);
        var bodyAnalyzer = new SyntaxNodeAnalyzer(builder, _config);
        var declarationAnalyzer = new DeclarationAnalyzer(builder, bodyAnalyzer, artifacts, _config);

        var numberOfCodeElements = codeGraph.Nodes.Count;
        _processedCodeElements = 0;
        _lastProgress = 0;

        // Take a snapshot of internal elements to avoid collection modification during iteration
        var internalElements = codeGraph.Nodes.Values.ToList();

        var options = new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism };
        Parallel.ForEach(internalElements, options, AnalyzeRelationshipsLocal);

        // After parallel processing, add all external elements to the graph
        builder.FlushExternalElementsToGraph();

        // Analyze global statements for each assembly
        AnalyzeGlobalStatementsForAssembly(solution, artifacts, builder, bodyAnalyzer);

        SendParserPhase2Progress(numberOfCodeElements, numberOfCodeElements);

        return Task.CompletedTask;

        void AnalyzeRelationshipsLocal(CodeElement element)
        {
            if (!artifacts.ElementIdToSymbolMap.TryGetValue(element.Id, out var symbol))
            {
                // INamespaceSymbol
                Interlocked.Increment(ref _processedCodeElements);
                return;
            }

            declarationAnalyzer.Analyze(solution, element, symbol);

            var loopValue = Interlocked.Increment(ref _processedCodeElements);
            SendParserPhase2Progress(loopValue, numberOfCodeElements);
        }
    }

    /// <summary>
    ///     Global statements (top-level code) have no containing method or type, so a synthetic
    ///     "GlobalStatements.Execute" class/method pair per assembly hosts their dependencies.
    /// </summary>
    private static void AnalyzeGlobalStatementsForAssembly(Solution solution, Artifacts artifacts,
        RelationshipBuilder builder, SyntaxNodeAnalyzer bodyAnalyzer)
    {
        foreach (var (assemblySymbol, globalStatements) in artifacts.GlobalStatementsByAssembly)
        {
            if (globalStatements.Count == 0)
            {
                continue;
            }

            // Find the existing assembly element
            var symbolKey = assemblySymbol.Key();
            var assemblyElement = artifacts.SymbolKeyToElementMap[symbolKey];

            // Create a dummy class for this assembly's global statements
            var dummyClassId = Guid.NewGuid().ToString();
            const string dummyClassName = "GlobalStatements";
            var dummyClassFullName = assemblySymbol.BuildSymbolName() + "." + dummyClassName;
            var dummyClass = new CodeElement(dummyClassId, CodeElementType.Class, dummyClassName, dummyClassFullName,
                assemblyElement);
            builder.AddElement(dummyClass, assemblyElement);

            // Create a dummy method to contain global statements
            var dummyMethodId = Guid.NewGuid().ToString();
            const string dummyMethodName = "Execute";
            var dummyMethodFullName = $"{dummyClassFullName}.{dummyMethodName}";
            var dummyMethod = new CodeElement(dummyMethodId, CodeElementType.Method, dummyMethodName,
                dummyMethodFullName, dummyClass);
            builder.AddElement(dummyMethod, dummyClass);

            // Analyze global statements within the context of the dummy method
            foreach (var globalStatement in globalStatements)
            {
                var document = solution.GetDocument(globalStatement.SyntaxTree);
                var semanticModel = document?.GetSemanticModelAsync().Result;
                if (semanticModel != null)
                {
                    bodyAnalyzer.AnalyzeMethodBody(dummyMethod, globalStatement, semanticModel);
                }
            }
        }
    }

    private void SendParserPhase2Progress(int processed, int total)
    {
        var currentProgress = (long)Math.Floor(processed / (double)total * 100);
        var lastReported = Interlocked.Read(ref _lastProgress);

        if (currentProgress > lastReported)
        {
            if (Interlocked.CompareExchange(ref _lastProgress, currentProgress, lastReported) == lastReported)
            {
                var msg = $"Phase 2/2: Analyzing relationships. Finished {currentProgress}%.";
                _progress?.Report(msg);
            }
        }
    }
}
