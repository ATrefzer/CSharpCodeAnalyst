namespace CSharpLanguage;

internal class MyAttribute : Attribute
{
}

public interface IStructInterface
{
    void Method();
}

[My]
internal struct StructWithInterface : IStructInterface
{
    public void Method()
    {
        throw new NotImplementedException();
    }
}