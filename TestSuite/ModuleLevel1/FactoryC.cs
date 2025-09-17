namespace ModuleLevel1;

public class FactoryC
{
    public IServiceC Create()
    {
        // DEP_CREATE: Method Create creates instance of type ServiceC
        return new ServiceC();
    }
}