namespace CSharpLanguage;

internal class MoreGenerics
{
    private void Run()
    {
        var f1 = new Foo<int>();
        var f2 = new Foo<int, int, int>();

        M1(3);
        M1<int, int>(3);
        M2<string>(2, "2");
        M2<string>("2", 2);
    }

    private void M1<T>(T obj)
    {
    }

    private void M1<T, T2>(T obj)
    {
    }


    private void M2<T>(int i, T obj)
    {
    }

    private void M2<T>(T obj, int i)
    {
    }


    internal class Foo<T>
    {
    }

    internal class Foo<T, T2, T3>
    {
    }
}