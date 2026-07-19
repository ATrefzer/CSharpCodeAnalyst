using System.Diagnostics;
using CSharpCodeAnalyst.CodeGraph.Contracts;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeGraph.Metrics;
using CSharpCodeAnalyst.CodeParser.Parser.Config;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace CSharpCodeAnalyst.CodeParser.Parser;

/// <summary>
///     Parses a solution and builds a code graph.
///     There are two phases:
///     1. Find all code elements and their parent-child relationships.
///     2. Build the dependencies between the code elements.
/// </summary>
public class Parser(ParserConfig config, IProgress<string>? progress = null)
{

    /// <summary>
    ///     A small, curated set of framework reference assemblies, resolved once from the runtime
    ///     directory. Covers the common BCL surface (incl. LINQ) so typical snippets compile; types defined
    ///     in the snippet itself resolve regardless. Callers needing more can extend this later.
    /// </summary>
    private static readonly MetadataReference[] FrameworkReferences = BuildFrameworkReferences();

    private readonly ParserDiagnostics _diagnostics = new();

    public IParserDiagnostics Diagnostics
    {
        get => _diagnostics;
    }


    public async Task<ParseResult> ParseAsync(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();

        return extension switch
        {
            ".sln" or ".slnx" => await ParseSolution(path),
            ".csproj" => await ParseProject(path),
            ".cs" => await ParseSingleFile(path),
            _ => throw new ArgumentException($"Unsupported file type: {extension}. Expected .sln, .slnx, .csproj or .cs")
        };
    }

    /// <summary>
    ///     Parses a single C# file. Not surfaced as its own menu entry - a user can reach it from the
    ///     import dialog by typing a ".cs" file name into the file mask, which is handy for quickly
    ///     inspecting one file. Reads the text from disk and runs it through the in-memory pipeline
    ///     (<see cref="ParseSourceAsync" />), passing the real path so source locations stay navigable.
    /// </summary>
    private async Task<ParseResult> ParseSingleFile(string filePath)
    {
        var code = await File.ReadAllTextAsync(filePath);
        return await ParseSourceAsync(code, filePath);
    }

    /// <summary>
    ///     Parses a single project and builds a code graph.
    /// </summary>
    private async Task<ParseResult> ParseProject(string projectPath)
    {
        _diagnostics.Clear();
        var sw = Stopwatch.StartNew();

        progress?.Report("Compiling project ...");

        using var workspace = MSBuildWorkspace.Create();
        workspace.RegisterWorkspaceFailedHandler(WorkspaceFailedHandler);
        var project = await workspace.OpenProjectAsync(projectPath);

        // Create a solution from the single project
        var solution = project.Solution;

        sw.Stop();
        Trace.TraceInformation("Compiling: " + sw.Elapsed);

        return await ParseSolutionInternal(solution);
    }

    private void WorkspaceFailedHandler(WorkspaceDiagnosticEventArgs e)
    {
        _diagnostics.Add(e.Diagnostic);
        Trace.WriteLine(e.Diagnostic.Message);
    }

    /// <summary>
    ///     Parses a complete solution and builds a code graph.
    /// </summary>
    private async Task<ParseResult> ParseSolution(string solutionPath)
    {
        _diagnostics.Clear();
        var sw = Stopwatch.StartNew();

        progress?.Report("Compiling solution ...");

        using var workspace = MSBuildWorkspace.Create();
        workspace.RegisterWorkspaceFailedHandler(WorkspaceFailedHandler);
        var solution = await workspace.OpenSolutionAsync(solutionPath);

        sw.Stop();
        Trace.TraceInformation("Compiling: " + sw.Elapsed);

        return await ParseSolutionInternal(solution);
    }

    /// <summary>
    ///     Parses in-memory C# source through the full parser pipeline using a Roslyn
    ///     <see cref="AdhocWorkspace" /> - no MSBuild, no <see cref="Initializer.InitializeMsBuildLocator" />
    ///     and no disk access. Intended for unit tests and small tooling that want a real
    ///     <see cref="CodeGraph" /> from a code snippet.
    ///     The synthetic project/document file names are pure identifiers; none of them needs to exist on
    ///     disk. The only files read are the framework reference assemblies in the runtime directory.
    /// </summary>
    public Task<ParseResult> ParseSourceAsync(string code, string? documentPath = null)
    {
        _diagnostics.Clear();
        var solution = BuildAdhocSolution(code, documentPath ?? "InMemory.cs");
        return ParseSolutionInternal(solution);
    }

    private static Solution BuildAdhocSolution(string code, string documentPath)
    {
        var projectId = ProjectId.CreateNewId();

        // The project file path is a synthetic identifier, not a real file. A ".csproj" extension is
        // required so the project passes HierarchyAnalyzer.ShouldAnalyzeProject. The project name is fixed
        // (not derived from the document) so a user exclusion filter like ".*Tests" cannot accidentally
        // drop a single file named e.g. "FooTests.cs".
        var projectInfo = ProjectInfo.Create(projectId, VersionStamp.Default, "InMemory", "InMemory",
                LanguageNames.CSharp, "InMemory.csproj")
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithMetadataReferences(FrameworkReferences);

        var workspace = new AdhocWorkspace();
        workspace.AddProject(projectInfo);

        var documentId = DocumentId.CreateNewId(projectId);

        // The document path must be non-null so its syntax tree is recognized as a project file
        // (HierarchyAnalyzer.IsProjectFile). When a real file path is passed it also makes the resulting
        // SourceLocations point at that file, so "Jump to Code" works.
        // AddDocument returns a new (immutable) solution that contains the document - that is the one we
        // hand to the analyzers; workspace.CurrentSolution would not contain it.
        return workspace.CurrentSolution.AddDocument(documentId, Path.GetFileName(documentPath),
            SourceText.From(code), filePath: documentPath);
    }

    private static MetadataReference[] BuildFrameworkReferences()
    {
        var coreDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        string[] assemblyFileNames =
        [
            "System.Private.CoreLib.dll",
            "System.Runtime.dll",
            "System.Linq.dll",
            "System.Linq.Expressions.dll",
            "System.Collections.dll",
            "System.Console.dll",
            "netstandard.dll"
        ];

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { typeof(object).Assembly.Location };
        foreach (var fileName in assemblyFileNames)
        {
            var path = Path.Combine(coreDirectory, fileName);
            if (File.Exists(path))
            {
                paths.Add(path);
            }
        }

        return paths.Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToArray();
    }

    /// <summary>
    ///     Internal method that does the actual parsing work.
    /// </summary>
    private async Task<ParseResult> ParseSolutionInternal(Solution solution)
    {
        var sw = Stopwatch.StartNew();

        // First Pass: Build Hierarchy
        var phase1 = new HierarchyAnalyzer(progress, config, _diagnostics);
        var (codeGraph, artifacts) = await phase1.BuildHierarchy(solution);

        var metrics = CollectSourceMetrics(artifacts);

        sw.Stop();
        Trace.TraceInformation("Finding code elements: " + sw.Elapsed);
        sw = Stopwatch.StartNew();

        // Second Pass: Build Relationships
        var phase2 = new RelationshipAnalyzer(progress, config);
        await phase2.AnalyzeRelationships(solution, codeGraph, artifacts);

        sw.Stop();
        Trace.TraceInformation("Analyzing relationships: " + sw.Elapsed);

        // Makes the cycle detection easier because I never get to the assembly as shared ancestor
        // for a nested relationships.
        InsertGlobalNamespaceIfUsed(codeGraph);

#if DEBUG
        CodeGraphPlausibilityChecks.PlausibilityChecks(codeGraph);
#endif
        //await File.WriteAllTextAsync("d:\\debug0.txt", codeGraph.ToDebug());

        return new ParseResult(codeGraph, metrics);
    }


    /// <summary>
    ///     Computes per-member source metrics from the symbol map built in phase 1.
    ///     Only method-like symbols with an actual implementation are measured; abstract/extern/
    ///     interface declarations and body-less partial signatures are skipped.
    /// </summary>
    private MetricStore CollectSourceMetrics(Artifacts artifacts)
    {
        var metrics = new MetricStore();

        progress?.Report("Calculating source metrics");

        foreach (var (elementId, symbol) in artifacts.ElementIdToSymbolMap)
        {
            if (symbol is not IMethodSymbol methodSymbol)
            {
                continue;
            }

            // For a partial method measure the implementation part - the stored symbol may be the
            // body-less definition part (declaration order decides which one phase 1 kept).
            var bodySymbol = methodSymbol.PartialImplementationPart ?? methodSymbol;
            var syntax = bodySymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
            if (syntax is not null && SourceMetricsCollector.HasBody(syntax))
            {
                metrics.Add(elementId, SourceMetricsCollector.ComputeForMethodDeclaration(syntax));
            }
        }

        return metrics;
    }

    /// <summary>
    ///     If any assembly uses the global namespace we add the global namespace to all assemblies.
    ///     For example a unit test assembly may have the autogenerated Main.
    /// </summary>
    private static void InsertGlobalNamespaceIfUsed(CodeGraph.Graph.CodeGraph codeGraph)
    {
        const string global = CodeElement.GlobalNamespaceName;
        var assemblies = codeGraph.GetRoots();
        Debug.Assert(assemblies.All(a => a.ElementType == CodeElementType.Assembly));
        var isGlobalNsUsed = assemblies.Any(a => a.Children.Any(c => c.ElementType != CodeElementType.Namespace));

        var newGlobalNamespaces = new List<CodeElement>();
        if (isGlobalNsUsed)
        {
            foreach (var assembly in assemblies)
            {
                var childrenCopy = assembly.Children.ToList();

                var id = Guid.NewGuid().ToString();
                var fullName = assembly.FullName + "." + global;
                var globalNs = new CodeElement(id, CodeElementType.Namespace, global, fullName, assembly) { IsExternal = assembly.IsExternal };
                newGlobalNamespaces.Add(globalNs);

                assembly.Children.Add(globalNs);

                // Move elements
                foreach (var child in childrenCopy)
                {
                    child.MoveTo(globalNs);
                }
            }

            // Don't modify collection during iteration
            foreach (var globalNs in newGlobalNamespaces)
            {
                codeGraph.Nodes[globalNs.Id] = globalNs;
            }
        }
    }
}