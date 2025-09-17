namespace Extensions.ModuleLevel.ProjectC
{
    public interface IConfigService
    {
        string GetConfiguration();
    }

    public class ConfigService : IConfigService
    {
        public string GetConfiguration()
        {
            return ConstantsC.DefaultConfig;
        }
    }

    public class DataProcessor
    {
        public void Process(string data)
        {
            // Process data
        }
    }

    public static class ConstantsC
    {
        public const string DefaultConfig = "default";
        
        public enum ConfigType
        {
            Standard,
            Advanced
        }
    }
}