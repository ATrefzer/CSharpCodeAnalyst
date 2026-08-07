// ============================================================================
// PIPELINE COMMENTARY 2.0 - Solution and repository analyzer
// ============================================================================

using System.Collections.Concurrent;
using System.Xml.Linq;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;

namespace PipelineDocsCli;

internal sealed class RepositoryPipelineAnalyzer
{
    public static async Task<PipelineSnapshot> AnalyzeAsync(string repositoryRoot, string? solutionPath, CancellationToken cancellationToken)
    {
        repositoryRoot = Path.GetFullPath(repositoryRoot);
        solutionPath = ResolveSolution(repositoryRoot, solutionPath);

        if (!MSBuildLocator.IsRegistered)
        {
            MSBuildLocator.RegisterDefaults();
        }

        var diagnostics = new ConcurrentBag<PipelineDiagnostic>();
        using var workspace = MSBuildWorkspace.Create();
        workspace.RegisterWorkspaceFailedHandler(e =>
        {
            diagnostics.Add(new PipelineDiagnostic(
                e.Diagnostic.Kind.ToString(), "MSBuildWorkspace", e.Diagnostic.Message));
        });

        var solution = await workspace.OpenSolutionAsync(solutionPath, cancellationToken: cancellationToken);
        var solutionProjects = solution.Projects
            .Where(p => !string.IsNullOrWhiteSpace(p.FilePath))
            .ToDictionary(p => Path.GetFullPath(p.FilePath!), StringComparer.OrdinalIgnoreCase);

        var allProjectPaths = Directory.EnumerateFiles(repositoryRoot, "*.*proj", SearchOption.AllDirectories)
            .Where(path => !IsIgnored(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var projects = new List<ProjectPipeline>();
        var referenceMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var reverseReferenceMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var projectPath in allProjectPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var metadata = ReadProjectMetadata(projectPath, repositoryRoot, diagnostics);
            referenceMap[projectPath] = metadata.References.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var reference in metadata.References)
            {
                if (!reverseReferenceMap.TryGetValue(reference, out var callers))
                {
                    callers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    reverseReferenceMap[reference] = callers;
                }
                callers.Add(projectPath);
            }
        }

        foreach (var projectPath in allProjectPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var metadata = ReadProjectMetadata(projectPath, repositoryRoot, diagnostics);
            var isSolutionMember = solutionProjects.TryGetValue(projectPath, out var roslynProject);
            var typePipelines = isSolutionMember && roslynProject is not null
                ? await AnalyzeProjectAsync(roslynProject, repositoryRoot, diagnostics, cancellationToken)
                : Array.Empty<TypePipeline>();

            var referencedBy = reverseReferenceMap.TryGetValue(projectPath, out var callers)
                ? callers.Select(path => Path.GetFileNameWithoutExtension(path)).OrderBy(x => x).ToArray()
                : Array.Empty<string>();

            var classifications = ClassifyProject(metadata, isSolutionMember, referencedBy.Length > 0);
            projects.Add(new ProjectPipeline(
                metadata.Name,
                Path.GetRelativePath(repositoryRoot, projectPath).Replace('\\', '/'),
                isSolutionMember,
                metadata.OutputKind,
                metadata.TargetFramework,
                metadata.References
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name!)
                    .OrderBy(name => name)
                    .ToArray(),
                referencedBy,
                typePipelines,
                classifications,
                metadata.Purpose));
        }

        return new PipelineSnapshot(
            repositoryRoot,
            Path.GetRelativePath(repositoryRoot, solutionPath).Replace('\\', '/'),
            DateTimeOffset.UtcNow,
            projects,
            diagnostics.OrderBy(d => d.Scope).ThenBy(d => d.Message).ToArray());
    }

    private static async Task<IReadOnlyList<TypePipeline>> AnalyzeProjectAsync(
        Project project,
        string repositoryRoot,
        ConcurrentBag<PipelineDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation is null)
        {
            diagnostics.Add(new PipelineDiagnostic("Warning", project.Name, "Compilation was unavailable; project topology only was emitted."));
            return Array.Empty<TypePipeline>();
        }

        var interim = new Dictionary<ISymbol, MutableTypePipeline>(SymbolEqualityComparer.Default);
        foreach (var document in project.Documents.Where(d => d.SupportsSyntaxTree && d.FilePath is not null))
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var model = await document.GetSemanticModelAsync(cancellationToken);
            if (root is null || model is null) continue;

            foreach (var declaration in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(declaration, cancellationToken) is not INamedTypeSymbol typeSymbol) continue;
                var item = new MutableTypePipeline(typeSymbol, Path.GetRelativePath(repositoryRoot, document.FilePath!).Replace('\\', '/'));
                item.Implements.UnionWith(typeSymbol.Interfaces.Select(i => i.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                if (typeSymbol.BaseType is { SpecialType: SpecialType.None } baseType)
                {
                    item.Implements.Add(baseType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
                }

                foreach (var invocation in declaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol method)
                    {
                        item.Calls.Add(method.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat));
                    }
                }

                foreach (var creation in declaration.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
                {
                    if (model.GetSymbolInfo(creation, cancellationToken).Symbol is IMethodSymbol constructor)
                    {
                        item.Creates.Add(constructor.ContainingType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat));
                    }
                }

                foreach (var assignment in declaration.DescendantNodes().OfType<AssignmentExpressionSyntax>())
                {
                    if (!assignment.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AddAssignmentExpression) &&
                        !assignment.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SubtractAssignmentExpression)) continue;
                    var symbol = model.GetSymbolInfo(assignment.Left, cancellationToken).Symbol;
                    if (symbol is IEventSymbol eventSymbol)
                    {
                        item.Events.Add(eventSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat));
                    }
                }

                foreach (var methodDeclaration in declaration.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    if (model.GetDeclaredSymbol(methodDeclaration, cancellationToken) is not IMethodSymbol method) continue;
                    item.Inputs.UnionWith(method.Parameters.Select(p => p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                    if (!method.ReturnsVoid)
                    {
                        item.Outputs.Add(method.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
                    }
                }

                interim[typeSymbol] = item;
            }
        }

        foreach (var pair in interim)
        {
            var references = await SymbolFinder.FindReferencesAsync(pair.Key, project.Solution, cancellationToken);
            foreach (var reference in references.SelectMany(r => r.Locations))
            {
                var doc = project.Solution.GetDocument(reference.Document.Id);
                if (doc?.FilePath is null || doc.Project.Id == project.Id && doc.FilePath == pair.Value.File) continue;
                pair.Value.CalledBy.Add(Path.GetFileNameWithoutExtension(doc.FilePath));
            }
        }

        return interim.Values
            .OrderBy(v => v.Symbol.ToDisplayString(), StringComparer.Ordinal)
            .Select(v => v.Freeze())
            .ToArray();
    }

    private static ProjectMetadata ReadProjectMetadata(string projectPath, string repositoryRoot, ConcurrentBag<PipelineDiagnostic> diagnostics)
    {
        try
        {
            var document = XDocument.Load(projectPath);
            string Value(string name) => document.Descendants().FirstOrDefault(e => e.Name.LocalName == name)?.Value.Trim() ?? string.Empty;
            var references = document.Descendants()
                .Where(e => e.Name.LocalName == "ProjectReference")
                .Select(e => e.Attribute("Include")?.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectPath)!, value!)))
                .ToArray();

            return new ProjectMetadata(
                Path.GetFileNameWithoutExtension(projectPath),
                Value("OutputType") is { Length: > 0 } output ? output : "Library",
                Value("TargetFramework") is { Length: > 0 } tfm ? tfm : Value("TargetFrameworks"),
                references,
                Value("PipelinePurpose") is { Length: > 0 } purpose ? purpose : null,
                Value("PipelineRole") is { Length: > 0 } role ? role : null);
        }
        catch (Exception ex)
        {
            diagnostics.Add(new PipelineDiagnostic("Warning", Path.GetRelativePath(repositoryRoot, projectPath), ex.Message));
            return new ProjectMetadata(Path.GetFileNameWithoutExtension(projectPath), "Unknown", "Unknown", Array.Empty<string>(), null, null);
        }
    }

    private static List<string> ClassifyProject(ProjectMetadata metadata, bool solutionMember, bool referenced)
    {
        var result = new List<string> { solutionMember ? "solution-member" : "adjacent-project" };
        if (metadata.Name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) || metadata.Name.Contains("Test", StringComparison.OrdinalIgnoreCase)) result.Add("test-project");
        if (metadata.OutputKind.Equals("Exe", StringComparison.OrdinalIgnoreCase) || metadata.OutputKind.Equals("WinExe", StringComparison.OrdinalIgnoreCase)) result.Add("executable");
        if (referenced) result.Add("project-referenced");
        if (!solutionMember && !referenced) result.Add(metadata.Role ?? "standalone-unclassified");
        if (!string.IsNullOrWhiteSpace(metadata.Role) && !result.Contains(metadata.Role, StringComparer.OrdinalIgnoreCase)) result.Add(metadata.Role);
        return result;
    }

    private static string ResolveSolution(string repositoryRoot, string? solutionPath)
    {
        if (!string.IsNullOrWhiteSpace(solutionPath))
        {
            return Path.GetFullPath(Path.IsPathRooted(solutionPath) ? solutionPath : Path.Combine(repositoryRoot, solutionPath));
        }

        return Directory.EnumerateFiles(repositoryRoot, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault()
            ?? throw new FileNotFoundException($"No solution file was found in {repositoryRoot}.");
    }

    private static bool IsIgnored(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
        path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
        path.Contains($"{Path.DirectorySeparatorChar}archive{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
        path.Contains($"{Path.DirectorySeparatorChar}backup{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    private sealed record ProjectMetadata(string Name, string OutputKind, string TargetFramework, IReadOnlyList<string> References, string? Purpose, string? Role);

    private sealed class MutableTypePipeline
    {
        public MutableTypePipeline(INamedTypeSymbol symbol, string file) { Symbol = symbol; File = file; }
        public INamedTypeSymbol Symbol { get; }
        public string File { get; }
        public HashSet<string> Implements { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Calls { get; } = new(StringComparer.Ordinal);
        public HashSet<string> CalledBy { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Creates { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Events { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Inputs { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Outputs { get; } = new(StringComparer.Ordinal);

        public TypePipeline Freeze() => new(
            Symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            Symbol.TypeKind.ToString(),
            File,
            Implements.OrderBy(x => x).ToArray(),
            Calls.OrderBy(x => x).ToArray(),
            CalledBy.OrderBy(x => x).ToArray(),
            Creates.OrderBy(x => x).ToArray(),
            Events.OrderBy(x => x).ToArray(),
            Inputs.OrderBy(x => x).ToArray(),
            Outputs.OrderBy(x => x).ToArray());
    }
}
