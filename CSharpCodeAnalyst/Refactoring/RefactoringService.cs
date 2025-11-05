using System.Diagnostics;
using CodeGraph.Graph;
using CSharpCodeAnalyst.Messages;
using CSharpCodeAnalyst.Resources;
using CSharpCodeAnalyst.Shared.Contracts;

namespace CSharpCodeAnalyst.Refactoring;

/// <summary>
///     Service for virtual refactorings - modifying the code graph without changing source code.
///     Useful for experimenting with architecture changes and analyzing impact before refactoring.
/// </summary>
public class RefactoringService
{
    private readonly IPublisher _messaging;
    private readonly RefactoringInteraction _refactoringInteraction;
    private CodeGraph.Graph.CodeGraph? _graph;
    private CodeElement? _target;


    public RefactoringService(RefactoringInteraction refactoringInteraction, IPublisher messaging)
    {
        _refactoringInteraction = refactoringInteraction;
        _messaging = messaging;
    }

    public void LoadCodeGraph(CodeGraph.Graph.CodeGraph graph)
    {
        _graph = graph;
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

    public void CreateCodeElement(string? parentId)
    {
        if (_graph is null)
        {
            return;
        }

        if (!CanCreateCodeElement(parentId))
        {
            return;
        }

        var parent = FindCodeElement(parentId);
        var naming = new CodeElementNaming(_graph, parent);
        var validChildTypes = GetValidChildTypes(parent);
        var specs = _refactoringInteraction.AskUserForCodeElementSpecs(parent, validChildTypes, naming);
        if (specs == null)
        {
            return;
        }

        var id = Guid.NewGuid().ToString();
        var element = new CodeElement(id, specs.ElementType, specs.Name, GetFullName(specs.Name, parent), parent)
        {
            IsExternal = false
        };

        var result = _graph.IntegrateCodeElementFromOriginal(element);

        // Important: Return the cloned element that is actually integrated into the graph.
        if (result.IsAdded)
        {
            _messaging.Publish<CodeGraphRefactored>(new CodeElementCreated(_graph, result.CodeElement));
        }
    }

    private CodeElement? FindCodeElement(string? id)
    {
        if (_graph is null)
        {
            return null;
        }

        return id == null ? null : _graph.Nodes[id];
    }


    private static string GetFullName(string name, CodeElement? parent)
    {
        if (parent == null || string.IsNullOrEmpty(parent.FullName))
        {
            return name;
        }

        return $"{parent.FullName}.{name}";
    }

    public bool CanCreateCodeElement(string? parentId)
    {
        if (_graph is null)
        {
            return false;
        }

        var parent = FindCodeElement(parentId);
        var validChildren = GetValidChildTypes(parent);
        return validChildren.Count > 0;
    }

    public void DeleteCodeElementAndAllChildren(string? elementId)
    {
        if (_graph is null || elementId is null)
        {
            return;
        }

        if (!_refactoringInteraction.AskUserToProceed(Strings.Refactoring_DeleteFromModel_Message))
        {
            return;
        }

        // Augment infos about delete elemets
        var element = FindCodeElement(elementId);
        var parentId = element?.Parent?.Id;
        var deletedIds = _graph.DeleteCodeElementAndAllChildren(elementId);
        if (deletedIds.Any())
        {
            _messaging.Publish<CodeGraphRefactored>(new CodeElementsDeleted(_graph, elementId, parentId, deletedIds));
        }

        if (_target != null && deletedIds.Contains(_target.Id))
        {
            _target = null;
        }
    }



    public bool CanMoveCodeElements(HashSet<string> sourceIds)
    {
        if (_graph is null)
        {
            return false;
        }

        if (!sourceIds.Any() || _target is null)
        {
            return false;
        }

        var involved = new List<CodeElement>();
        foreach (var id in sourceIds)
        {
            if (!_graph.Nodes.TryGetValue(id, out var source))
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

            // Check if target is a child of source (would create circular hierarchy)
            if (_target.IsChildOf(source))
            {
                return false;
            }

            var validChildTypesForParent = GetValidChildTypes(_target);
            if (!validChildTypesForParent.Contains(source.ElementType))
            {
                return false;
            }

            // Don't move overlapping elements.
            if (involved.Any(i => i.IsChildOf(source) || i.IsParentOf(source)))
            {
                return false;
            }

            involved.Add(source);
        }

        return true;
    }

    public bool CanSetMovementTarget(string? elementId)
    {
        if (_graph is null || elementId is null)
        {
            return false;
        }

        var codeElement = _graph.Nodes[elementId];

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

    public bool SetMovementTarget(string? targetId)
    {
        if (_graph is null || targetId is null)
        {
            return false;
        }

        var target = _graph.Nodes[targetId];
        if (!CanSetMovementTarget(targetId))
        {
            return false;
        }

        _target = target;
        return true;
    }


    public void MoveCodeElements(HashSet<string> sourceIds)
    {
        if (_graph is null || !sourceIds.Any() || _target is null)
        {
            return;
        }

        if (!CanMoveCodeElements(sourceIds))
        {
            return;
        }

        if (!_refactoringInteraction.AskUserToProceed(Strings.Refactoring_Move_Message))
        {
            return;
        }

        foreach (var sourceId in sourceIds)
        {
            var source = _graph.Nodes[sourceId];
            source.MoveTo(_target!);

            Debug.Assert(source.ElementType != CodeElementType.Assembly && source.Parent != null);
        }


        _messaging.Publish<CodeGraphRefactored>(new CodeElementsMoved(_graph, sourceIds, _target.Id));
    }

    public CodeElement? GetMovementTarget()
    {
        return _target;
    }

    public void DeleteRelationships(List<Relationship> relationships)
    {
        if (_graph is null || !relationships.Any())
        {
            return;
        }

        if (!_refactoringInteraction.AskUserToProceed(Strings.Refactoring_DeleteRelationships_Message))
        {
            return;
        }

        _graph.DeleteRelationships(relationships);
        _messaging.Publish<CodeGraphRefactored>(new RelationshipsDeleted(_graph, relationships));
    }
}