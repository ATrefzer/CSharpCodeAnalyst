using CodeParser.Extensions;
using Contracts.Graph;
using CSharpCodeAnalyst.Resources;

namespace CSharpCodeAnalyst.Refactoring;

/// <summary>
///     Service for virtual refactorings - modifying the code graph without changing source code.
///     Useful for experimenting with architecture changes and analyzing impact before refactoring.
/// </summary>
public class RefactoringService
{
    private readonly RefactoringInteraction _refactoringInteraction;
    private CodeElement? _target;


    public RefactoringService(RefactoringInteraction refactoringInteraction)
    {
        _refactoringInteraction = refactoringInteraction;
    }


    /// <summary>
    ///     Gets the valid code element types that can be created as children of the given parent.
    /// </summary>
    private List<CodeElementType> GetValidChildTypes(CodeElement? parent)
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
                CodeElementType.Class, // Nested classes
                CodeElementType.Struct, // Nested structs
                CodeElementType.Interface, // Nested interfaces
                CodeElementType.Enum, // Nested enums
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
                CodeElementType.Field // Enum values
            ],

            // Methods, properties, fields, events, delegates cannot have children
            _ => []
        };
    }

    public CodeElement? CreateCodeElement(CodeGraph? codeGraph, CodeElement? parent)
    {
        if (codeGraph is null)
        {
            return null;
        }

        if (!CanCreateCodeElement(parent))
        {
            return null;
        }

        var naming = new CodeElementNaming(codeGraph, parent);
        var validChildTypes = GetValidChildTypes(parent);
        var specs = _refactoringInteraction.AskUserForCodeElementSpecs(parent, validChildTypes, naming);
        if (specs == null)
        {
            return null;
        }

        var id = Guid.NewGuid().ToString();
        var element = new CodeElement(id, specs.ElementType, specs.Name, GetFullName(specs.Name, parent), parent)
        {
            IsExternal = false
        };

        codeGraph.IntegrateCodeElementFromOriginal(element);
        return element;



        // TODO Graph was changed! Update TreeView, Advanced SearchView, Canvas is ok, it does not know the element.
    }




    private static string GetFullName(string name, CodeElement? parent)
    {
        if (parent == null || string.IsNullOrEmpty(parent.FullName))
        {
            return name;
        }

        return $"{parent.FullName}.{name}";
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

    public HashSet<string> DeleteCodeElementAndAllChildren(CodeGraph? codeGraph, string id)
    {
        if (codeGraph is null)
        {
            return [];
        }

        if (!_refactoringInteraction.AskUserToProceed(Strings.DeleteFromModel_Message))
        {
            return [];
        }

        return codeGraph.DeleteCodeElementAndAllChildren(id);
    }

    public bool CanMoveCodeElement(CodeElement? source)
    {
        if (source is null || _target is null)
        {
            return false;
        }
        
        return true;
    }

    public bool CanSetMovementTarget(CodeElement? tvmCodeElement)
    {
        return true;
    }

    public bool SetMovementTarget(CodeElement? target)
    {
        if (!CanSetMovementTarget(target))
        {
            return false;
        }

        _target = target;
        return true;
    }


    public bool MoveCodeElement(CodeElement? source)
    {
        if (!CanMoveCodeElement(source))
        {
            return false;
        }

        source!.MoveTo(_target!);
        return true;
    }

    public CodeElement? GetMovementTarget()
    {
        return _target;
    }
}