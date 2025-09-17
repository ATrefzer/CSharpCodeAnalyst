using Contracts.Graph;

namespace CodeParserTests.Helper;

public class TestCodeGraph : CodeGraph
{
    public CodeElement CreateNamespace(string id, CodeElement? parent = null)
    {
        var element = new CodeElement(id, CodeElementType.Namespace, id, id, parent);
        Link(parent, element);
        return element;
    }

    private void Link(CodeElement? parent, CodeElement element)
    {
        parent?.Children.Add(element);
        Nodes[element.Id] = element;
    }

    public CodeElement CreateClass(string id, CodeElement? parent = null)
    {
        var element = new CodeElement(id, CodeElementType.Class, id, id, parent);
        Link(parent, element);
        return element;
    }

    public CodeElement CreateInterface(string id, CodeElement? parent = null)
    {
        var element = new CodeElement(id, CodeElementType.Interface, id, id, parent);
        Link(parent, element);
        return element;
    }

    public CodeElement CreateMethod(string id, CodeElement? parent = null)
    {
        var element = new CodeElement(id, CodeElementType.Method, id, id, parent);
        Link(parent, element);
        return element;
    }


    public CodeElement CreateField(string id, CodeElement? parent = null)
    {
        var element = new CodeElement(id, CodeElementType.Field, id, id, parent);
        Link(parent, element);
        return element;
    }

    public CodeElement CreateEnum(string id, CodeElement? parent = null)
    {
        var element = new CodeElement(id, CodeElementType.Enum, id, id, parent);
        Link(parent, element);
        return element;
    }
}