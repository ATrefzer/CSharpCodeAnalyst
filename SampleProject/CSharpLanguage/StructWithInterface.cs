namespace CSharpLanguage;

internal class MyAttribute : Attribute
{
}

internal class My2Attribute : Attribute
{
}

public interface IStructInterface
{
    void Method(int i);
}

[My]
internal struct StructWithInterface : IStructInterface
{
    [My]
    public void Method([My2] int i)
    {
        throw new NotImplementedException();
    }
}