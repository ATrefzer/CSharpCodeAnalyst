using ModuleLevel0;

namespace ModuleLevel2;

public class InterfaceImplementerInDifferentCompilation : InterfaceInDifferentCompilation
{
    public event EventHandler AEvent;


    public void Method()
    {
        throw new NotImplementedException();
    }
}