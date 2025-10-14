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
    private static List<CodeElementType> GetValidChildTypes(CodeElement? parent)
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
                CodeElementType.Delegate,
                CodeElementType.Namespace
            ],

            CodeElementType.Class =>
            [
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

        var result = codeGraph.IntegrateCodeElementFromOriginal(element);
        
        // Important: Return the cloned element that is actually integrated into the graph.
        return result.CodeElement;
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
        var validChildren = GetValidChildTypes(parent);
        return validChildren.Count > 0;
    }

    public HashSet<string> DeleteCodeElementAndAllChildren(CodeGraph? codeGraph, string id)
    {
        if (codeGraph is null)
        {
            return [];
        }

        if (!_refactoringInteraction.AskUserToProceed(Strings.Refactoring_DeleteFromModel_Message))
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

        if (source.ElementType is CodeElementType.Assembly)
        {
            return false;
        }

        if (_target.Id == source.Id)
        {
            return false;
        }

        var validChildTypesForParent = GetValidChildTypes(_target);
        return validChildTypesForParent.Contains(source.ElementType);
    }

    public bool CanSetMovementTarget(CodeElement? codeElement)
    {
        if (codeElement is null)
        {
            return false;
        }

        if (codeElement.ElementType is 
            CodeElementType.Field or
            CodeElementType.Event or
            CodeElementType.Delegate or
            CodeElementType.Method or
            CodeElementType.Other or
            CodeElementType.Enum or
            CodeElementType.Property)
            return false;

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
        
        if (!_refactoringInteraction.AskUserToProceed(Strings.Refactoring_Move_Message))
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