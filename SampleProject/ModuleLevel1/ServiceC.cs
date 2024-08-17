using ModuleLevel1.Model;
using ModuleLevel2;

namespace ModuleLevel1
{
    // DEP_IMPLEMENTS
    // DEP_DERIVES
    internal class ServiceC : ServiceBase 
    {
        public override void Do(int v)
        {
            var data = new ModelA(new ModelB());
            var modelC = data.GetModelC();

            var foo = () => data.GetModelD();

            Utility.UtilityMethod1();
            Execute();
        }

        public override int IfProperty { get; set; }

        void Execute()
        {
            // DEP_CALLS_EXTERNAL
            Console.WriteLine("ServiceC.Execute");
        }
    }
}
