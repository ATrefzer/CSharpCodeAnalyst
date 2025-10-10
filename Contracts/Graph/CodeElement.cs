using System.Diagnostics;

namespace Contracts.Graph;

[DebuggerDisplay("{ElementType}: {Name} {(IsExternal ? \"(External)\" : \"\")}")]
public class CodeElement(string id, CodeElementType elementType, string name, string fullName, CodeElement? parent)
{
    public List<SourceLocation> SourceLocations { get; set; } = [];

    /// <summary>
    ///     Unlike in the graph where external relationships are omitted
    ///     I want to keep all attributes here.
    /// </summary>
    public HashSet<string> Attributes { get; set; } = [];

    public HashSet<CodeElement> Children { get; } = [];

    public HashSet<Relationship> Relationships { get; } = [];

    public CodeElementType ElementType { get; } = elementType;

    public string Id { get; } = id;

    public string Name { get; } = name;
    public string FullName { get; private set; } = fullName;

    public CodeElement? Parent { get; set; } = parent;

    /// <summary>
    ///     Indicates whether this code element is defined outside the solution.
    ///     External elements are from framework types, NuGet packages, or other referenced assemblies.
    ///     External elements are treated as leaf nodes - their internal dependencies are not analyzed.
    /// </summary>
    public bool IsExternal { get; init; }

    public override bool Equals(object? obj)
    {
        if (obj != null && obj.GetType() == GetType())
        {
            var other = (CodeElement)obj;
            return Id == other.Id;
        }

        return false;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public string GetFullPath(bool omitGlobalNamespace = false)
    {
        var names = new List<string> { Name };
        var current = Parent;
        while (current != null)
        {
            if (!omitGlobalNamespace || !IsGlobalNamespace(current))
            {
                names.Insert(0, current.Name);
            }

            current = current.Parent;
        }

        return string.Join(".", names);

        bool IsGlobalNamespace(CodeElement codeElement)
        {
            return codeElement.Parent?.ElementType == CodeElementType.Assembly 
                   && codeElement.ElementType == CodeElementType.Namespace 
                   && codeElement.Name == "global";
        }
    }

    /// <summary>
    ///     Index 0 is the root.
    /// </summary>
    public List<CodeElement> GetPathToRoot(bool includeSelf)
    {
        var path = new List<CodeElement>();


        var current = includeSelf ? this : Parent;
        while (current != null)
        {
            path.Add(current);
            current = current.Parent;
        }

        path.Reverse();
        return path;
    }

    /// <summary>
    ///     No parent, no children, no dependencies.
    /// </summary>
    public CodeElement CloneSimple()
    {
        var element = new CodeElement(Id, ElementType, Name,
            FullName, null)
        {
            IsExternal = IsExternal
        };

        element.SourceLocations.AddRange(SourceLocations);
        return element;
    }

    public bool IsChildOf(CodeElement parent)
    {
        if (Id == parent.Id)
        {
            return false;
        }

        var current = this;
        while (current != null)
        {
            if (current.Id == parent.Id)
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }

    public bool IsParentOf(CodeElement child)
    {
        return child.IsChildOf(this);
    }

    public HashSet<string> GetChildrenIncludingSelf()
    {
        var childrenIncludingSelf = new HashSet<string>();
        var stack = new Stack<CodeElement>();
        stack.Push(this);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (childrenIncludingSelf.Add(current.Id))
            {
                foreach (var child in current.Children)
                {
                    stack.Push(child);
                }
            }
        }

        return childrenIncludingSelf;
    }

    /// <summary>
    ///     Moves the CodeElement to the new parent.
    /// </summary>
    public void MoveTo(CodeElement newParent)
    {
        ArgumentNullException.ThrowIfNull(newParent, nameof(newParent));

        // Remove child from old parent
        var oldParent = Parent;
        oldParent?.Children.RemoveWhere(c => c.Id == Id);

        // Set new parent
        Parent = newParent;
        newParent.Children.Add(this);
        
        // Update full name 
        Traversal.Dfs(newParent, n => n.FullName = n.GetFullPath());
    }
}