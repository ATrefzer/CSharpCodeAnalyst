namespace ModuleLevel2;

public class Utility
{
    public static void UtilityMethod1()
    {
        UtilityMethod2();
    }

    private static void UtilityMethod2()
    {
        UtilityMethod1();
    }
}