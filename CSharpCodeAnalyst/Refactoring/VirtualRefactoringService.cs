using Contracts.Graph;

namespace CSharpCodeAnalyst.Refactoring;

/// <summary>
/// Service for virtual refactorings - modifying the code graph without changing source code.
/// Useful for experimenting with architecture changes and analyzing impact before refactoring.
/// </summary>
public class VirtualRefactoringService
{
    private readonly CodeGraph _codeGraph;
    private int _virtualElementCounter = 0;

    public VirtualRefactoringService(CodeGraph codeGraph)
    {
        _codeGraph = codeGraph;
    }


    /// <summary>
    /// Gets the valid code element types that can be created as children of the given parent.
    /// </summary>
    public List<CodeElementType> GetValidChildTypes(CodeElement? parent)
    {
        if (parent == null)
        {
            // Root level - can create assemblies
            return [CodeElementType.Assembly];
        }

        return parent.ElementType switch
        {
            CodeElementType.Assembly =>
            [
                CodeElementType.Namespace
            ],

            CodeElementType.Namespace =>
            [
                CodeElementType.Class,
                CodeElementType.Interface,
                CodeElementType.Struct,
                CodeElementType.Enum,
                CodeElementType.Record,
                CodeElementType.Delegate
            ],

            CodeElementType.Class =>
            [
                CodeElementType.Class,     // Nested classes
                CodeElementType.Struct,    // Nested structs
                CodeElementType.Interface, // Nested interfaces
                CodeElementType.Enum,      // Nested enums
                CodeElementType.Method,
                CodeElementType.Property,
                CodeElementType.Field,
                CodeElementType.Event,
                CodeElementType.Delegate
            ],

            CodeElementType.Struct =>
            [
                CodeElementType.Method,
                CodeElementType.Property,
                CodeElementType.Field,
                CodeElementType.Event
            ],

            CodeElementType.Interface =>
            [
                CodeElementType.Method,
                CodeElementType.Property,
                CodeElementType.Event
            ],

            CodeElementType.Enum =>
            [
                CodeElementType.Field  // Enum values
            ],

            // Methods, properties, fields, events, delegates cannot have children
            _ => []
        };
    }

    /// <summary>
    /// Creates a new virtual code element as a child of the specified parent.
    /// </summary>
    public CodeElement CreateVirtualElement(CodeElementType elementType, string name, CodeElement? parent)
    {
        // Generate a unique ID for the virtual element
        var id = $"virtual_{elementType}_{_virtualElementCounter++}_{Guid.NewGuid():N}";

        // Create the element
        var element = new CodeElement(id, elementType, name, GetFullName(name, parent), parent)
        {
            IsExternal = false
        };

        // Add to graph
        _codeGraph.IntegrateCodeElementFromOriginal(element);

        // If parent exists, add as child
        if (parent != null)
        {
            parent.Children.Add(element);
        }

        return element;
    }

    /// <summary>
    /// Generates a user-friendly default name for a new element.
    /// </summary>
    public string GetDefaultName(CodeElementType elementType, CodeElement? parent)
    {
        var baseName = elementType switch
        {
            CodeElementType.Assembly => "NewAssembly",
            CodeElementType.Namespace => "NewNamespace",
            CodeElementType.Class => "NewClass",
            CodeElementType.Interface => "INewInterface",
            CodeElementType.Struct => "NewStruct",
            CodeElementType.Enum => "NewEnum",
            CodeElementType.Record => "NewRecord",
            CodeElementType.Delegate => "NewDelegate",
            CodeElementType.Method => "NewMethod",
            CodeElementType.Property => "NewProperty",
            CodeElementType.Field => "newField",
            CodeElementType.Event => "NewEvent",
            _ => "NewElement"
        };

        // Find a unique name by appending numbers if needed
        var counter = 1;
        var candidateName = baseName;
        var parentPath = parent?.FullName ?? string.Empty;

        while (_codeGraph.Nodes.Values.Any(n =>
            n.Name == candidateName &&
            (n.Parent?.FullName ?? string.Empty) == parentPath))
        {
            candidateName = $"{baseName}{counter++}";
        }

        return candidateName;
    }

    private static string GetFullName(string name, CodeElement? parent)
    {
        if (parent == null)
        {
            return name;
        }

        return string.IsNullOrEmpty(parent.FullName)
            ? name
            : $"{parent.FullName}.{name}";
    }

    public static bool CanCreateCodeElement(CodeElement? parent)
    {
        if (parent is null)
        {
            // We can create an assembly
            return true;
        }

        return parent.ElementType is CodeElementType.Assembly or
            CodeElementType.Namespace or
            CodeElementType.Class or
            CodeElementType.Interface or
            CodeElementType.Record or
            CodeElementType.Struct;
    }
}
