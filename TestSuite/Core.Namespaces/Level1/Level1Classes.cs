using Core.Namespaces.Level1.Level2;

namespace Core.Namespaces.Level1;

public class Level1Class
{
    private Level2Class? _level2;

    public void DoSomething()
    {
        _level2?.ProcessData();
    }

    public void CreateLevel2()
    {
        _level2 = new Level2Class();
    }
}

public class AnotherLevel1Class
{
    public void WorkWithRoot()
    {
        var root = new RootClass();
        root.UseLevel1();
    }
}