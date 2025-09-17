namespace Core.Namespaces.Level1.Level2.Level3;

public class Level3Class
{
    public void DeepOperation()
    {
        // Deep nested operation
        var root = new RootClass();
        root.UseLevel1();
    }
}

public class DeepestClass
{
    public void ReachToTop()
    {
        var level1 = new Level1Class();
        level1.DoSomething();
    }
}