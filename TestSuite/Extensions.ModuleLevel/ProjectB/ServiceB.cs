using Extensions.ModuleLevel.ProjectC;

namespace Extensions.ModuleLevel.ProjectB
{
    public class ServiceB
    {
        public void ProcessB(string config)
        {
            var processor = new DataProcessor();
            processor.Process(config);
        }
    }

    public class ModelB
    {
        public string Name { get; set; } = string.Empty;
        public ConstantsC.ConfigType Type { get; set; }
    }
}