namespace ModuleLevel1;

internal abstract class ServiceBase : IServiceC
{
    public virtual void Do(int v)
    {
        throw new NotImplementedException();
    }

    public virtual int IfProperty { get; set; }
}