using System.Diagnostics;
using Contracts.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeParser.Parser;

public partial class Parser
{
    private readonly List<INamedTypeSymbol> _allNamedTypesInSolution = new();

    private readonly Dictionary<IAssemblySymbol, List<GlobalStatementSyntax>> _globalStatementsByAssembly =
        new(SymbolEqualityComparer.Default);


    private async Task BuildHierarchy(Solution solution)
    {
        foreach (var project in solution.Projects)
        {
            if (_config.IsProjectIncluded(project.Name) is false)
            {
                continue;
            }

            var compilation = await project.GetCompilationAsync();
            if (compilation == null)
            {
                Trace.WriteLine($"No compilation found for project: {project.Name}");
                continue;
            }

            // Build also a list of all named types in the solution
            // We need this in phase 2 to resolve dependencies
            // Constructed types are not contained in this list!
            var types = compilation.GetSymbolsWithName(_ => true, SymbolFilter.Type).OfType<INamedTypeSymbol>();
            _allNamedTypesInSolution.AddRange(types);


            BuildHierarchy(compilation);
        }
    }

    private void BuildHierarchy(Compilation compilation)
    {
        // Assembly has no source location.
        var assemblySymbol = compilation.Assembly;
        var assemblyElement = GetOrCreateCodeElement(assemblySymbol, CodeElementType.Assembly, null!, null!);
        _globalStatementsByAssembly[assemblySymbol] = new List<GlobalStatementSyntax>();

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

        var location = GetLocation(node);

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
                symbol = semanticModel.GetDeclaredSymbol(node) as IMethodSymbol;
                elementType = CodeElementType.Method; // or you could create a separate Constructor type
                break;

            case FieldDeclarationSyntax fieldDeclaration:
                foreach (var variable in fieldDeclaration.Declaration.Variables)
                {
                    if (semanticModel.GetDeclaredSymbol(variable) is IFieldSymbol fieldSymbol)
                    {
                        var fieldLocation = GetLocation(variable);
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
                        var eventLocation = GetLocation(variable);
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
}