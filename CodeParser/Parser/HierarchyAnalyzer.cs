using System.Diagnostics;
using CodeGraph.Graph;
using CodeParser.Parser.Config;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeParser.Parser;

/// <summary>
///     Extracts all code elements found in the solution together with other artifacts needed for phase 2.
/// </summary>
public class HierarchyAnalyzer
{
    private readonly List<INamedTypeSymbol> _allNamedTypesInSolution = [];
    private readonly CodeGraph.Graph.CodeGraph _codeGraph = new();
    private readonly ParserConfig _config;
    private readonly Dictionary<string, ISymbol> _elementIdToSymbolMap = new();

    private readonly Dictionary<IAssemblySymbol, List<GlobalStatementSyntax>> _globalStatementsByAssembly =
        new(SymbolEqualityComparer.Default);

    private readonly Progress _progress;
    private readonly HashSet<string> _projectFilePaths = [];
    private readonly Dictionary<string, CodeElement> _symbolKeyToElementMap = new();

    public HierarchyAnalyzer(Progress progress, ParserConfig config)
    {
        _progress = progress;
        _config = config;
    }

    public async Task<(CodeGraph.Graph.CodeGraph codeGraph, Artifacts artifacts)> BuildHierarchy(Solution solution)
    {
        CollectAllFilePathInSolution(solution);

        var projects = await GetValidProjects(solution);
        foreach (var project in projects)
        {
            var compilation = await project.GetCompilationAsync();
            if (compilation == null)
            {
                Trace.WriteLine($"No compilation found for project: {project.Name}");
                continue;
            }

            // Build also a list of all named types in the solution
            // We need this in phase 2 to resolve relationships
            // Constructed types are not contained in this list!
            var types = compilation.GetSymbolsWithName(_ => true, SymbolFilter.Type).OfType<INamedTypeSymbol>();
            _allNamedTypesInSolution.AddRange(types);

            BuildHierarchy(compilation);
        }

        var result = new Artifacts(
            _allNamedTypesInSolution.AsReadOnly(),
            _elementIdToSymbolMap.AsReadOnly(),
            _globalStatementsByAssembly.AsReadOnly(),
            _symbolKeyToElementMap.AsReadOnly());
        return (_codeGraph, result);
    }

    /// <summary>
    ///     Remove all projects that do not pass our include filter or cannot be parsed.
    /// </summary>
    private async Task<List<Project>> GetValidProjects(Solution solution)
    {
        // At the moment I cannot handle more than one project with the same assembly name
        // So I remove them.

        var assemblyNameToProject = new Dictionary<string, Project>();
        var duplicates = new HashSet<string>();
        foreach (var project in solution.Projects)
        {
            // Regular expression patterns.
            if (!_config.IsProjectIncluded(project.Name))
            {
                continue;
            }

            var compilation = await project.GetCompilationAsync();
            if (compilation != null)
            {
                var assemblyName = compilation.Assembly.Name;
                if (assemblyNameToProject.ContainsKey(assemblyName))
                {
                    duplicates.Add(assemblyName);
                }

                assemblyNameToProject[assemblyName] = project;
            }
        }

        foreach (var name in duplicates)
        {
            assemblyNameToProject.Remove(name);
            Trace.WriteLine($"Removed assembly with duplicate name in solution: {name}");
        }

        return assemblyNameToProject.Values.ToList();
    }

    private void BuildHierarchy(Compilation compilation)
    {
        // Assembly has no source location.
        var assemblySymbol = compilation.Assembly;
        var assemblyElement = GetOrCreateCodeElement(assemblySymbol, CodeElementType.Assembly, null!, null!);
        _globalStatementsByAssembly[assemblySymbol] = [];

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            if (!IsProjectFile(syntaxTree.FilePath))
            {
                continue;
            }

            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();


            ProcessNodeForHierarchy(root, semanticModel, assemblyElement);
        }
    }

    private void ProcessNodeForHierarchy(SyntaxNode node, SemanticModel semanticModel,
        CodeElement parent)
    {
        ISymbol? symbol = null;
        var elementType = CodeElementType.Other;

        var location = node.GetSyntaxLocation();

        switch (node)
        {
            case CompilationUnitSyntax:
                // CompilationUnitSyntax is the root of the syntax tree, so we don't need to create a CodeElement for it
                symbol = null;
                break;
            case FileScopedNamespaceDeclarationSyntax:
            case NamespaceDeclarationSyntax:
                // Newer C#10 allows to omit the curly brackets for a namespace definition.
                // This is a new syntax and has to be handled separately beside NamespaceDeclarationSyntax
                symbol = semanticModel.GetDeclaredSymbol(node) as INamespaceSymbol;
                elementType = CodeElementType.Namespace;
                break;
            case ClassDeclarationSyntax:
                symbol = semanticModel.GetDeclaredSymbol(node) as INamedTypeSymbol;
                elementType = CodeElementType.Class;
                break;
            case RecordDeclarationSyntax:
                symbol = semanticModel.GetDeclaredSymbol(node) as INamedTypeSymbol;
                elementType = CodeElementType.Record;
                break;
            case InterfaceDeclarationSyntax:
                symbol = semanticModel.GetDeclaredSymbol(node) as INamedTypeSymbol;
                elementType = CodeElementType.Interface;
                break;
            case StructDeclarationSyntax:
                symbol = semanticModel.GetDeclaredSymbol(node) as INamedTypeSymbol;
                elementType = CodeElementType.Struct;
                break;
            case EnumDeclarationSyntax:
                symbol = semanticModel.GetDeclaredSymbol(node) as INamedTypeSymbol;
                elementType = CodeElementType.Enum;
                break;
            case MethodDeclarationSyntax:
                symbol = semanticModel.GetDeclaredSymbol(node) as IMethodSymbol;
                elementType = CodeElementType.Method;
                break;
            case ConstructorDeclarationSyntax:

                // Does not include primary constructors.
                // Normal constructor: symbol.DeclaringSyntaxReferences → ConstructorDeclarationSyntax.
                // Primary constructor: symbol.DeclaringSyntaxReferences → TypeDeclarationSyntax (z. B. ClassDeclarationSyntax), with ParameterList.

                symbol = semanticModel.GetDeclaredSymbol(node) as IMethodSymbol;
                elementType = CodeElementType.Method; // or you could create a separate Constructor type
                break;

            case FieldDeclarationSyntax fieldDeclaration:
                foreach (var variable in fieldDeclaration.Declaration.Variables)
                {
                    if (semanticModel.GetDeclaredSymbol(variable) is IFieldSymbol fieldSymbol)
                    {
                        var fieldLocation = variable.GetSyntaxLocation();
                        var fieldElement =
                            GetOrCreateCodeElement(fieldSymbol, CodeElementType.Field, parent, fieldLocation);
                    }
                }

                return; // We've handled the fields, so we can return
            case PropertyDeclarationSyntax:
                symbol = semanticModel.GetDeclaredSymbol(node) as IPropertySymbol;
                elementType = CodeElementType.Property;
                break;
            case DelegateDeclarationSyntax:
                symbol = semanticModel.GetDeclaredSymbol(node) as INamedTypeSymbol;
                elementType = CodeElementType.Delegate;
                break;

            case EventFieldDeclarationSyntax eventFieldDeclaration:

                // public event EventHandler MyEvent;
                foreach (var variable in eventFieldDeclaration.Declaration.Variables)
                {
                    if (semanticModel.GetDeclaredSymbol(variable) is IEventSymbol eventSymbol)
                    {
                        var eventLocation = variable.GetSyntaxLocation();
                        var eventElement =
                            GetOrCreateCodeElement(eventSymbol, CodeElementType.Event, parent, eventLocation);
                    }
                }

                return; // We've handled the event fields, so we can return

            case EventDeclarationSyntax:
                symbol = semanticModel.GetDeclaredSymbol(node) as IEventSymbol;
                elementType = CodeElementType.Event;
                break;

            case GlobalStatementSyntax globalStatementSyntax:
                var assemblySymbol = semanticModel.Compilation.Assembly;
                _globalStatementsByAssembly[assemblySymbol].Add(globalStatementSyntax);
                return; // We'll handle these collectively later

            // Add more cases as needed (e.g., for events, delegates, etc.)
        }

        if (symbol != null)
        {
            var element = GetOrCreateCodeElementWithNamespaceHierarchy(symbol, elementType, parent, location);
            foreach (var childNode in node.ChildNodes())
            {
                ProcessNodeForHierarchy(childNode, semanticModel, element);
            }
        }
        else
        {
            // The parent gets the indirect children assigned as children
            foreach (var childNode in node.ChildNodes())
            {
                ProcessNodeForHierarchy(childNode, semanticModel, parent);
            }
        }
    }

    private bool IsProjectFile(string filePath)
    {
        return _projectFilePaths.Contains(filePath);
    }

    /// <summary>
    ///     Since I iterate over the compilation units (to get rid of external code)
    ///     any seen namespace, even "namespace X.Y.Z;", ends up as
    ///     namespace Z directly under the assembly node.
    ///     So If I see namespace X.Y.Z I create X, Y, Z and set them as parent child.
    /// </summary>
    private CodeElement GetOrCreateCodeElementWithNamespaceHierarchy(ISymbol symbol,
        CodeElementType elementType, CodeElement initialParent, SourceLocation? location)
    {
        if (symbol is INamespaceSymbol namespaceSymbol)
        {
            var namespaces = new Stack<INamespaceSymbol>();
            var current = namespaceSymbol;

            // Build the stack of nested namespaces
            while (current is { IsGlobalNamespace: false })
            {
                namespaces.Push(current);
                current = current.ContainingNamespace;
            }

            var parent = initialParent;

            // Create or get each namespace in the hierarchy
            while (namespaces.Count > 0)
            {
                // We create the whole chain when encountering namespace X.Y.Z;
                // So I give all the same source location. Right?
                var ns = namespaces.Pop();

                // The location is only valid for the input namespace symbol.
                var nsLocation = ReferenceEquals(ns, namespaceSymbol) ? location : null;
                var nsElement = GetOrCreateCodeElement(ns, CodeElementType.Namespace, parent, nsLocation);
                parent = nsElement;
            }

            return parent;
        }

        // For non-namespace symbols, use the original logic
        return GetOrCreateCodeElement(symbol, elementType, initialParent, location);
    }

    /// <summary>
    ///     Note: We store the symbol used to build the hierarchy.
    ///     If used in different a compilation unit the symbol may be another instance.
    /// </summary>
    private CodeElement GetOrCreateCodeElement(ISymbol symbol, CodeElementType elementType, CodeElement? parent,
        SourceLocation? location)
    {
        var symbolKey = symbol.Key();

        // We may encounter namespace declarations in many files.
        if (_symbolKeyToElementMap.TryGetValue(symbolKey, out var existingElement))
        {
            UpdateCodeElementLocations(existingElement, location);
            WarnIfCodeElementHasMultipleSymbols(symbol, existingElement);
            return existingElement;
        }

        var name = symbol.Name;
        var fullName = symbol.BuildSymbolName();
        var newId = Guid.NewGuid().ToString();

        var element = new CodeElement(newId, elementType, name, fullName, parent);

        UpdateCodeElementLocations(element, location);

        parent?.Children.Add(element);
        _codeGraph.Nodes[element.Id] = element;
        _symbolKeyToElementMap[symbolKey] = element;

        // We need the symbol in phase2 when analyzing the relationships.
        if (symbol is not INamespaceSymbol)
        {
            _elementIdToSymbolMap[element.Id] = symbol;
        }

        SendParserPhase1Progress(_codeGraph.Nodes.Count);

        return element;
    }

    private void WarnIfCodeElementHasMultipleSymbols(ISymbol symbol, CodeElement existingElement)
    {
        if (symbol is not INamespaceSymbol)
        {
            // Get warning if we have different symbols for the same element.
            if (!_elementIdToSymbolMap[existingElement.Id].Equals(symbol, SymbolEqualityComparer.Default))
            {
                // Happens if two projects in the solution have the same name.
                // You lose one of them.
                Trace.WriteLine("(!) Found element with multiple symbols: " + symbol.ToDisplayString());
            }
        }
    }

    private static void UpdateCodeElementLocations(CodeElement element, SourceLocation? location)
    {
        if (element.ElementType == CodeElementType.Namespace)
        {
            // Namespaces are spread over many files,
            // and it is useless for the user to see all of them.
            return;
        }

        if (location != null)
        {
            element.SourceLocations.Add(location);
        }
    }

    private void SendParserPhase1Progress(int numberOfCodeElements)
    {
        if (numberOfCodeElements % 10 == 0)
        {
            var msg = $"Phase 1/2: Already found {numberOfCodeElements} code elements.";
            _progress.SendProgress(msg);
        }
    }


    private void CollectAllFilePathInSolution(Solution solution)
    {
        foreach (var project in solution.Projects)
        {
            if (IsUnrecognizedProject(project.FilePath))
            {
                continue;
            }

            if (!_config.IsProjectIncluded(project.Name))
            {
                continue;
            }

            foreach (var document in project.Documents)
            {
                if (document.FilePath != null)
                {
                    _projectFilePaths.Add(document.FilePath);
                }
            }
        }
    }

    private bool IsUnrecognizedProject(string? projectFilePath)
    {
        var unrecognized = new List<string>
        {
            ".vbproj",
            ".fsproj",
            ".vcxproj",
            ".proj"
        };

        var ext = Path.GetExtension(projectFilePath);
        if (ext == null)
        {
            return false;
        }

        return unrecognized.Contains(ext);
    }
}