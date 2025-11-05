using System.Diagnostics;
using CodeGraph.Graph;

namespace CSharpCodeAnalyst.Refactoring;

public class CodeElementNaming(CodeGraph.Graph.CodeGraph codeGraph, CodeElement? parent) : ICodeElementNaming
{
    /// <summary>
    ///     Generates a user-friendly default name for a new element.
    /// </summary>
    public string GetDefaultName(CodeElementType elementType)
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
        var candidateName = baseName;
        var counter = 1;
        while (!IsNameUnique(elementType, candidateName))
        {
            candidateName = $"{baseName}{counter++}";
        }

        return candidateName;
    }

    public bool IsValid(CodeElementType type, string name)
    {
        return IsNameUnique(type, name);
    }


    private bool IsNameUnique(CodeElementType elementType, string newName)
    {
        List<string> occupiedNames;
        if (parent is null)
        {
            Debug.Assert(elementType is CodeElementType.Assembly);
            occupiedNames = codeGraph.GetRoots().Select(r => r.Name).ToList();
        }
        else
        {
            occupiedNames = parent.Children.Select(c => c.Name).ToList();
        }

        return !occupiedNames.Contains(newName);
    }
}