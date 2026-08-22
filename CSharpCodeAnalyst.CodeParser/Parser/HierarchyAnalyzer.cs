using System.Diagnostics;
using CSharpCodeAnalyst.CodeGraph.Graph;
using CSharpCodeAnalyst.CodeParser.Parser.Config;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpCodeAnalyst.CodeParser.Parser;

/// <summary>
///     Extracts all code elements found in the solution together with other artifacts needed for phase 2.
/// </summary>
public class HierarchyAnalyzer
{
    private readonly List<INamedTypeSymbol> _allNamedTypesInSolution = [];
    private readonly CodeGraph.Graph.CodeGraph _codeGraph = new();
    private readonly ParserConfig _config;

    private readonly ParserDiagnostics _diagnostics;
    private readonly Dictionary<string, ISymbol> _elementIdToSymbolMap = new();

    private readonly Dictionary<IAssemblySymbol, List<GlobalStatementSyntax>> _globalStatementsByAssembly =
        new(SymbolEqualityComparer.Default);

    private readonly IProgress<string>? _progress;
    private readonly HashSet<string> _projectFilePaths = [];

    /// <summary>The files a tool wrote, collected while walking - see <see cref="MarkGeneratedElements" />.</summary>
    private readonly HashSet<string> _generatedFilePaths = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, CodeElement> _symbolKeyToElementMap = new();

    internal HierarchyAnalyzer(IProgress<string>? progress, ParserConfig config, ParserDiagnostics diagnostics)
    {
        _progress = progress;
        _config = config;
        _diagnostics = diagnostics;
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

            // Source-generated documents (e.g. CommunityToolkit.Mvvm [ObservableProperty]/[RelayCommand],
            // [GeneratedRegex], ...) are not part of project.Documents and their syntax trees are not in
            // the compilation, so they have to be requested explicitly.
            // Always, and deliberately so: leaving them out does not just hide the generated members, it
            // removes the only reference many hand-written ones have. Nothing else reads the backing field
            // of an [ObservableProperty], and nothing else calls the method behind a [RelayCommand] - they
            // would all turn into dead code. What is generated is marked instead (see MarkGeneratedElements).
            var generatedDocuments = await project.GetSourceGeneratedDocumentsAsync();

            // Build also a list of all named types in the solution
            // We need this in phase 2 to resolve relationships
            // Constructed types are not contained in this list!
            var types = compilation.GetSymbolsWithName(_ => true, SymbolFilter.Type).OfType<INamedTypeSymbol>();
            _allNamedTypesInSolution.AddRange(types);

            await BuildHierarchy(compilation, generatedDocuments);
        }

        MarkGeneratedElements();

        var result = new Artifacts(
            _allNamedTypesInSolution.AsReadOnly(),
            _elementIdToSymbolMap.AsReadOnly(),
            _globalStatementsByAssembly.AsReadOnly(),
            _symbolKeyToElementMap.AsReadOnly(),
            BuildInterfaceImplementations(_allNamedTypesInSolution).AsReadOnly());
        return (_codeGraph, result);
    }

    /// <summary>
    ///     Builds the interface-key -> implementing-types lookup once, so phase 2 does not rebuild a Key()
    ///     string per type per interface for every interface member. Each (type, interface) pair contributes
    ///     once, mirroring the previous per-lookup scan over AllInterfaces.
    /// </summary>
    private static Dictionary<string, List<INamedTypeSymbol>> BuildInterfaceImplementations(
        IEnumerable<INamedTypeSymbol> allNamedTypes)
    {
        var map = new Dictionary<string, List<INamedTypeSymbol>>();
        foreach (var type in allNamedTypes)
        {
            // AllInterfaces also includes interfaces implemented in a base type - same as the old scan.
            foreach (var interfaceSymbol in type.AllInterfaces)
            {
                // AllInterfaces holds the CONSTRUCTED interfaces (IHandler<Item>), but phase 2 looks the
                // map up with the interface member's containing type, which is the DEFINITION
                // (IHandler<T>). Key the map by the definition, otherwise closed generic implementations
                // are never found and their member Implements edges are lost.
                var interfaceKey = interfaceSymbol.OriginalDefinition.Key();
                if (!map.TryGetValue(interfaceKey, out var implementingTypes))
                {
                    implementingTypes = [];
                    map[interfaceKey] = implementingTypes;
                }

                // A type implementing several constructions of the same generic interface
                // (IHandler<A>, IHandler<B>) would be added once per construction under the same key.
                if (implementingTypes.Count == 0 || !ReferenceEquals(implementingTypes[^1], type))
                {
                    implementingTypes.Add(type);
                }
            }
        }

        return map;
    }

    /// <summary>
    ///     Remove all projects that do not pass our include filter or cannot be parsed.
    /// </summary>
    private async Task<List<Project>> GetValidProjects(Solution solution)
    {
        // We can only keep one project per assembly name (the symbol key is built from the assembly name).
        // The two reasons for duplicates - multi-targeting vs. a real name collision - are distinguished
        // and reported in ProjectSelector. Here we only map to/from the Roslyn Project type.

        var candidateToProject = new Dictionary<ProjectCandidate, Project>();
        var candidates = new List<ProjectCandidate>();
        foreach (var project in solution.Projects)
        {
            if (!ShouldAnalyzeProject(project))
            {
                continue;
            }

            var compilation = await project.GetCompilationAsync();
            if (compilation != null)
            {
                // Project name contains the target (net10)
                var candidate = new ProjectCandidate(compilation.Assembly.Name, project.FilePath, project.Name);
                candidates.Add(candidate);
                candidateToProject[candidate] = project;
            }
        }

        var selection = ProjectSelector.SelectProjectsPerAssembly(candidates);

        foreach (var warning in selection.Warnings)
        {
            _diagnostics.AddWarning(warning);
            Trace.WriteLine(warning);
        }

        foreach (var failure in selection.Failures)
        {
            _diagnostics.AddFailure(failure);
            Trace.WriteLine(failure);
        }

        return selection.Selected.Select(candidate => candidateToProject[candidate]).ToList();
    }

    private bool ShouldAnalyzeProject(Project project)
    {
        // Whitelist: only C# projects (.csproj) are analyzed. Other project types (.vbproj, .fsproj,
        // .sqlproj, .esproj, ...) may still yield a compilation; without this filter they would add an
        // empty assembly node and leak their types into AllNamedTypesInSolution.
        // The in-memory pipeline (Parser.BuildAdhocSolution) relies on this and uses a synthetic
        // ".csproj" project file name.
        if (!IsCSharpProject(project.FilePath))
        {
            return false;
        }

        // Regular expression patterns.
        if (!_config.IsProjectIncluded(project.Name))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Roslyn's accessibility onto ours. <c>NotApplicable</c> (namespaces, and anything Roslyn cannot
    ///     decide) maps to Unknown - the graph must not claim a visibility that does not exist.
    /// </summary>
    private static CodeGraph.Graph.AccessLevel MapAccessLevel(
        Microsoft.CodeAnalysis.Accessibility accessibility)
    {
        return accessibility switch
        {
            Microsoft.CodeAnalysis.Accessibility.Private => CodeGraph.Graph.AccessLevel.Private,
            Microsoft.CodeAnalysis.Accessibility.Protected => CodeGraph.Graph.AccessLevel.Protected,
            Microsoft.CodeAnalysis.Accessibility.Internal => CodeGraph.Graph.AccessLevel.Internal,
            Microsoft.CodeAnalysis.Accessibility.ProtectedAndInternal => CodeGraph.Graph.AccessLevel
                .ProtectedAndInternal,
            Microsoft.CodeAnalysis.Accessibility.ProtectedOrInternal => CodeGraph.Graph.AccessLevel
                .ProtectedOrInternal,
            Microsoft.CodeAnalysis.Accessibility.Public => CodeGraph.Graph.AccessLevel.Public,
            _ => CodeGraph.Graph.AccessLevel.Unknown
        };
    }

    private async Task BuildHierarchy(Compilation compilation, IEnumerable<Document> generatedDocuments)
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

            if (GeneratedCode.IsGeneratedFile(syntaxTree))
            {
                _generatedFilePaths.Add(syntaxTree.FilePath);
            }

            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();


            ProcessNodeForHierarchy(root, semanticModel, assemblyElement);
        }

        // Process the source-generated documents through the same hierarchy walk. The generated members
        // then get their own code element instead of being collapsed onto the containing type via the
        // phase-2 fallback. Generated members extend existing partial types, so the named types are
        // already collected above.
        foreach (var generatedDocument in generatedDocuments)
        {
            var semanticModel = await generatedDocument.GetSemanticModelAsync();
            var root = await generatedDocument.GetSyntaxRootAsync();
            if (semanticModel == null || root == null)
            {
                continue;
            }

            // Generated by definition - no file-name or header check needed.
            if (!string.IsNullOrEmpty(root.SyntaxTree.FilePath))
            {
                _generatedFilePaths.Add(root.SyntaxTree.FilePath);
            }

            ProcessNodeForHierarchy(root, semanticModel, assemblyElement);
        }
    }

    /// <summary>
    ///     Flags what a tool wrote. Runs after the whole solution is walked, because the decision needs
    ///     <i>all</i> declarations of an element: a WPF code-behind class is partial and lives in both a
    ///     generated and a hand-written file, and only the members that exist nowhere but the generated
    ///     half are generated (see <see cref="GeneratedCode.IsGeneratedElement" />).
    /// </summary>
    private void MarkGeneratedElements()
    {
        if (_generatedFilePaths.Count == 0)
        {
            return;
        }

        foreach (var element in _codeGraph.Nodes.Values)
        {
            element.IsGenerated = GeneratedCode.IsGeneratedElement(element, _generatedFilePaths);
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

            case OperatorDeclarationSyntax:
            case ConversionOperatorDeclarationSyntax:
            case DestructorDeclarationSyntax:

                // User-defined operators (operator +), conversions (implicit/explicit operator)
                // and finalizers (~Foo). All map to an IMethodSymbol; phase 2 walks their bodies
                // through DeclaringSyntaxReferences just like ordinary methods.
                symbol = semanticModel.GetDeclaredSymbol(node) as IMethodSymbol;
                elementType = CodeElementType.Method;
                break;

            case FieldDeclarationSyntax fieldDeclaration:
                foreach (var variable in fieldDeclaration.Declaration.Variables)
                {
                    if (semanticModel.GetDeclaredSymbol(variable) is IFieldSymbol fieldSymbol)
                    {
                        var fieldLocation = variable.GetSyntaxLocation();
                        var _ = GetOrCreateCodeElement(fieldSymbol, CodeElementType.Field, parent, fieldLocation);
                    }
                }

                return; // We've handled the fields, so we can return
            case PropertyDeclarationSyntax:
                symbol = semanticModel.GetDeclaredSymbol(node) as IPropertySymbol;
                elementType = CodeElementType.Property;
                break;
            case IndexerDeclarationSyntax:

                // An indexer is a property with parameters. Its symbol name is "this[]".
                symbol = semanticModel.GetDeclaredSymbol(node) as IPropertySymbol;
                elementType = CodeElementType.Property;
                break;

            case ParameterSyntax parameterSyntax:

                // A record's positional parameter declares a public property as well. Without this the
                // record is an empty type in the tree, and every use of "order.Id" falls back to the
                // type. Null for every other parameter, which then simply creates no element.
                symbol = FindPositionalProperty(parameterSyntax, semanticModel);
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
                        var _ = GetOrCreateCodeElement(eventSymbol, CodeElementType.Event, parent, eventLocation);
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

            // Split the property into get/set accessor children (when configured). Covers properties
            // and indexers, including auto-properties and record positional properties, because the
            // accessors are taken from the symbol, not the syntax.
            if (_config.SplitPropertyAccessors && symbol is IPropertySymbol propertySymbol)
            {
                CreatePropertyAccessorElements(propertySymbol, element);
            }

            if (symbol is INamedTypeSymbol namedTypeSymbol)
            {
                CreatePrimaryConstructorElement(namedTypeSymbol, node, element);
            }

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

    /// <summary>
    ///     The element for a primary constructor. It has no <see cref="ConstructorDeclarationSyntax" /> -
    ///     its declaring syntax is the type declaration itself - so the ordinary case never sees it, and
    ///     without this the constructor of every record and of every "class Foo(...)" is missing from the
    ///     graph while the long form has one.
    ///     <para>
    ///         A partial type is walked once per declaration; only one of them can carry the parameter
    ///         list, and <see cref="GetOrCreateCodeElement" /> deduplicates by symbol key anyway.
    ///     </para>
    /// </summary>
    private void CreatePrimaryConstructorElement(INamedTypeSymbol typeSymbol, SyntaxNode node, CodeElement typeElement)
    {
        if (node is not TypeDeclarationSyntax { ParameterList: { } parameterList })
        {
            return;
        }

        var primaryConstructor = typeSymbol.InstanceConstructors.FirstOrDefault(constructor =>
            !constructor.IsImplicitlyDeclared &&
            constructor.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() == node));

        if (primaryConstructor is null)
        {
            return;
        }

        // The parameter list, not the whole declaration - that is where the constructor is written.
        GetOrCreateCodeElement(primaryConstructor, CodeElementType.Method, typeElement, parameterList.GetSyntaxLocation());

        foreach (var parameter in primaryConstructor.Parameters)
        {
            CreateCapturedParameterElement(typeSymbol, parameter, typeElement);
        }
    }

    /// <summary>
    ///     The field a captured primary constructor parameter really is. "class Service(ILogger logger)"
    ///     with a method that uses <c>logger</c> stores it, and that storage is state shared by every
    ///     member touching it - without an element for it, two methods using the same parameter share
    ///     nothing and the type cohesion metric splits a class that is perfectly cohesive.
    ///     <para>
    ///         Whether a parameter is captured is the compiler's decision, and it is readable: it emits a
    ///         field named <c>&lt;name&gt;P</c>. A parameter that is never used gets none, and one used
    ///         only in a field initializer gets none either - there the declared field already carries
    ///         the state. A record's positional parameter produces a property backing field instead,
    ///         which carries an <see cref="IFieldSymbol.AssociatedSymbol" /> and is excluded here; the
    ///         property itself is the element (see <see cref="FindPositionalProperty" />).
    ///     </para>
    ///     <para>
    ///         The element is keyed on the <b>parameter</b>, not on that mangled field: a body referring
    ///         to <c>logger</c> binds to the parameter, so that is what phase 2 has to look up. It is a
    ///         child of the type rather than of the constructor, because the constructor is dropped from
    ///         the member graph as a lifecycle member and would take the field with it.
    ///     </para>
    /// </summary>
    private void CreateCapturedParameterElement(INamedTypeSymbol typeSymbol, IParameterSymbol parameter, CodeElement typeElement)
    {
        var captureFieldName = $"<{parameter.Name}>P";
        var isCaptured = typeSymbol.GetMembers()
            .OfType<IFieldSymbol>()
            .Any(field => field is { IsImplicitlyDeclared: true, AssociatedSymbol: null } && field.Name == captureFieldName);

        if (!isCaptured)
        {
            return;
        }

        var symbolKey = parameter.Key();
        if (_symbolKeyToElementMap.ContainsKey(symbolKey))
        {
            return;
        }

        var id = Guid.NewGuid().ToString();
        var element = new CodeElement(id, CodeElementType.Field, parameter.Name,
            typeElement.FullName + "." + parameter.Name, typeElement)
        {
            // The capture is not addressable from outside the type, whatever the parameter looks like.
            AccessLevel = CodeGraph.Graph.AccessLevel.Private
        };

        foreach (var parameterLocation in parameter.GetSymbolLocations())
        {
            element.SourceLocations.Add(parameterLocation);
        }

        typeElement.Children.Add(element);
        _codeGraph.Nodes[id] = element;
        _symbolKeyToElementMap[symbolKey] = element;
        _elementIdToSymbolMap[id] = parameter;
    }

    /// <summary>
    ///     The property a record's positional parameter declares, or null when the parameter declares
    ///     none - an ordinary method parameter, a primary constructor parameter of a class or struct
    ///     (those declare no member), or a record parameter whose property the type writes out itself.
    ///     <para>
    ///         Roslyn offers no direct route from the parameter to that property:
    ///         <c>GetDeclaredSymbol(ParameterSyntax)</c> yields the <see cref="IParameterSymbol" />. The
    ///         property is found the other way round - it is the one whose declaration <i>is</i> this
    ///         parameter. That also settles the write-it-out case: in
    ///         <c>record Order(int Id) { public int Id { get; init; } = Id; }</c> the member's declaring
    ///         syntax is the property declaration, so nothing is found here and the ordinary
    ///         <see cref="PropertyDeclarationSyntax" /> case creates it.
    ///     </para>
    /// </summary>
    private static IPropertySymbol? FindPositionalProperty(ParameterSyntax parameterSyntax, SemanticModel semanticModel)
    {
        if (parameterSyntax.Parent?.Parent is not TypeDeclarationSyntax)
        {
            // Not a primary constructor parameter at all.
            return null;
        }

        if (semanticModel.GetDeclaredSymbol(parameterSyntax) is not { } parameterSymbol)
        {
            return null;
        }

        return parameterSymbol.ContainingType?
            .GetMembers(parameterSymbol.Name)
            .OfType<IPropertySymbol>()
            .FirstOrDefault(property => property.DeclaringSyntaxReferences
                .Any(reference => reference.GetSyntax() == parameterSyntax));
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

        var name = symbol.GetDisplayName();
        var fullName = symbol.BuildSymbolName();
        var newId = Guid.NewGuid().ToString();

        var element = new CodeElement(newId, elementType, name, fullName, parent)
        {
            AccessLevel = MapAccessLevel(symbol.DeclaredAccessibility),
            MemberRole = symbol.GetMemberRole()
        };

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

    /// <summary>
    ///     Creates the getter/setter child elements for a property (or indexer). The accessors are taken
    ///     from the symbol (<see cref="IPropertySymbol.GetMethod" /> / <see cref="IPropertySymbol.SetMethod" />),
    ///     so auto-properties and synthesized record accessors are covered as well.
    /// </summary>
    private void CreatePropertyAccessorElements(IPropertySymbol propertySymbol, CodeElement propertyElement)
    {
        CreatePropertyAccessorElement(propertySymbol.GetMethod, propertyElement);
        CreatePropertyAccessorElement(propertySymbol.SetMethod, propertyElement);
    }

    private void CreatePropertyAccessorElement(IMethodSymbol? accessor, CodeElement propertyElement)
    {
        if (accessor is null)
        {
            return;
        }

        var symbolKey = accessor.Key();
        if (_symbolKeyToElementMap.ContainsKey(symbolKey))
        {
            // Partial type declared in several files - the accessor was already created.
            return;
        }

        // Roslyn names the accessors get_Prop / set_Prop. Keep that name so the node is self-describing
        // in flat views; the full name stays consistent with the tree path under the property.
        var name = accessor.Name;
        var fullName = propertyElement.FullName + "." + name;
        var id = Guid.NewGuid().ToString();
        var accessorElement = new CodeElement(id, CodeElementType.PropertyAccessor, name, fullName, propertyElement)
        {
            // An accessor may narrow the property ("public int P { get; private set; }").
            AccessLevel = MapAccessLevel(accessor.DeclaredAccessibility),
            MemberRole = accessor.GetMemberRole()
        };

        foreach (var accessorLocation in accessor.GetSymbolLocations())
        {
            accessorElement.SourceLocations.Add(accessorLocation);
        }

        propertyElement.Children.Add(accessorElement);
        _codeGraph.Nodes[id] = accessorElement;
        _symbolKeyToElementMap[symbolKey] = accessorElement;

        // Intentionally NOT added to _elementIdToSymbolMap: phase 2 handles the bodies on the property container
        // and routes each accessor body to its element. 
        // Not harmful but we would walk the properties accessor twice in phase 2.
        // 1. `AnalyzePropertyBody` of the Containers (Source `get_Prop`)
        // 2. `AnalyzeMethodRelationships` of the Accessors (Source `get_Prop`)
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
            _progress?.Report(msg);
        }
    }


    private void CollectAllFilePathInSolution(Solution solution)
    {
        foreach (var project in solution.Projects)
        {
            if (!ShouldAnalyzeProject(project))
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

    private static bool IsCSharpProject(string? projectFilePath)
    {
        return string.Equals(Path.GetExtension(projectFilePath), ".csproj", StringComparison.OrdinalIgnoreCase);
    }
}