using Core.Namespaces.Level1.Level2.Level3;

namespace Core.Namespaces.Level1.Level2;

public class Level2Class
{
    public void ProcessData()
    {
        var level3 = new Level3Class();
        level3.DeepOperation();
    }
}

public class Level2Processor
{
    public void ProcessWithLevel1()
    {
        var level1 = new Level1Class();
        level1.DoSomething();
    }
}