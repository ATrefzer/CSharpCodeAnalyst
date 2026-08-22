using System.Diagnostics;

namespace CSharpCodeAnalyst.CodeGraph.Graph;

[DebuggerDisplay("{ElementType}: {Name} {(IsExternal ? \"(External)\" : \"\")}")]
public class CodeElement(string id, CodeElementType elementType, string name, string fullName, CodeElement? parent)
{
    /// <summary>
    ///     Name of the synthetic namespace the parser inserts directly below an assembly for code that
    ///     lives in no namespace (see Parser.InsertGlobalNamespaceIfUsed). It is a modelling decision -
    ///     no element should sit directly under an assembly, which also keeps cycle detection simple -
    ///     so anything that takes paths from the user (architectural rules) must tolerate its absence.
    /// </summary>
    public const string GlobalNamespaceName = "global";

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

    /// <summary>
    ///     Whether a tool wrote this element rather than a person - every declaration of it sits in a
    ///     generated file. Generated code is always parsed, because it holds relationships nothing else
    ///     does; the flag exists so a result can leave out what nobody can edit anyway.
    ///     <para>
    ///         A partial type that a generator only extends is <i>not</i> marked: half of it is hand
    ///         written. Only what lives exclusively in the generated file is.
    ///     </para>
    /// </summary>
    public bool IsGenerated { get; set; }

    /// <summary>
    ///     How far the element can be reached from. <see cref="Graph.AccessLevel.Unknown" /> when the
    ///     producer does not supply it - every importer except the C# parser, and any project file written
    ///     before this existed. Never read Unknown as a value; it means "no information".
    /// </summary>
    public AccessLevel AccessLevel { get; init; }

    /// <summary>
    ///     What the member is there for - ordinary work, or construction / destruction of the object.
    ///     <see cref="Graph.MemberRole.Unknown" /> when the producer does not supply it, which is not the
    ///     same as <see cref="Graph.MemberRole.Normal" /> and must never be read as it. A graph whose
    ///     producer fills no roles simply has no lifecycle members; re-import it to get them.
    /// </summary>
    public MemberRole MemberRole { get; init; }

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
                   && codeElement is { ElementType: CodeElementType.Namespace, Name: GlobalNamespaceName };
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
            IsExternal = IsExternal,
            IsGenerated = IsGenerated,
            AccessLevel = AccessLevel,
            MemberRole = MemberRole
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

    /// <summary>
    ///     All elements of the subtree rooted at this element, including the element itself.
    /// </summary>
    public IEnumerable<CodeElement> GetSubtreeIncludingSelf()
    {
        var visited = new HashSet<string>();
        var stack = new Stack<CodeElement>();
        stack.Push(this);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visited.Add(current.Id))
            {
                continue;
            }

            yield return current;

            foreach (var child in current.Children)
            {
                stack.Push(child);
            }
        }
    }

    public HashSet<string> GetChildrenIncludingSelf()
    {
        return GetSubtreeIncludingSelf().Select(e => e.Id).ToHashSet();
    }

    public HashSet<string> GetChildren()
    {
        var children = GetChildrenIncludingSelf();
        children.Remove(Id);
        return children;
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

    private static class Traversal
    {
        public static void Dfs(CodeElement element, Action<CodeElement> handler)
        {
            HashSet<string> visited =
            [
                element.Id
            ];

            foreach (var child in element.Children)
            {
                if (!visited.Contains(child.Id))
                {
                    Dfs(child, visited, handler);
                }
            }

            handler(element);
        }


        private static void Dfs(CodeElement element, HashSet<string> visited, Action<CodeElement> handler)
        {
            visited.Add(element.Id);

            foreach (var child in element.Children)
            {
                if (!visited.Contains(child.Id))
                {
                    Dfs(child, visited, handler);
                }
            }

            handler(element);
        }
    }
}