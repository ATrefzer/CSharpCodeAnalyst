namespace CSharpLanguage.Regression_Hierarchies;

internal interface InterfaceBase
{
    void MethodFromInterfaceBase();
}

internal interface InterfaceA
{
    void MethodA();
}

internal interface InterfaceB
{
    void MethodB();
}

internal interface InterfaceC : InterfaceBase
{
    void MethodC();

    // TODO does it change the structure if we repeat it here?
}

internal class ClassBase : InterfaceA
{
    public virtual void MethodA()
    {
        throw new NotImplementedException();
    }
}

internal abstract class ClassDerived1 : ClassBase, InterfaceB, InterfaceC
{
    public virtual void MethodB()
    {
        throw new NotImplementedException();
    }

    public abstract void MethodC();

    public void MethodFromInterfaceBase()
    {
        throw new NotImplementedException();
    }
}

internal class ClassDerived2 : ClassDerived1
{
    public override void MethodA()
    {
        base.MethodA();
    }

    public override void MethodC()
    {
        throw new NotImplementedException();
    }
}

internal class ClassDerived3 : ClassDerived2
{
    public override void MethodB()
    {
        throw new NotImplementedException();
    }
}

internal class ClassDerived4 : ClassDerived3
{
    public override void MethodA()
    {
        base.MethodA();
    }
}