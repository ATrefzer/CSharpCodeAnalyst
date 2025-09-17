using Extensions.ModuleLevel.ProjectB;
using Extensions.ModuleLevel.ProjectC;

namespace Extensions.ModuleLevel.ProjectA
{
    public class ServiceA
    {
        private readonly ServiceB _serviceB;
        private readonly IConfigService _configService;

        public ServiceA(ServiceB serviceB, IConfigService configService)
        {
            _serviceB = serviceB;
            _configService = configService;
        }

        public void ProcessA()
        {
            var config = _configService.GetConfiguration();
            _serviceB.ProcessB(config);
        }
    }

    public class FactoryA
    {
        public ModelB CreateModelB()
        {
            return new ModelB();
        }
    }
}