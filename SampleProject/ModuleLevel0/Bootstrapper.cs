using ModuleLevel1;
using ModuleLevel2;


namespace ModuleLevel0
{
    public class Bootstrapper
    {
        public void Run()
        {
            var factory = new FactoryC();

            var obj = factory.Create();
            obj.Do(Constants.Constant1);
        }
    }
}
