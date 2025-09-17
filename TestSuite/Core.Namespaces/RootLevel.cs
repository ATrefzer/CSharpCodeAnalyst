using Core.Namespaces.Level1;
using Core.Namespaces.Level1.Level2;
using Core.Namespaces.Level1.Level2.Level3;

namespace Core.Namespaces;

public class RootClass
{
    public void UseLevel1()
    {
        var obj1 = new Level1Class();
        obj1.DoSomething();
    }

    public void UseLevel2()
    {
        var obj2 = new Level2Class();
        obj2.ProcessData();
    }

    public void UseLevel3()
    {
        var obj3 = new Level3Class();
        obj3.DeepOperation();
    }
}