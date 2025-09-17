using ModuleLevel1.Model;
using ModuleLevel2;

namespace ModuleLevel1;

// DEP_IMPLEMENTS
// DEP_DERIVES
internal class ServiceC : ServiceBase
{

    public override int IfProperty { get; set; }

    public override void Do(int v)
    {
        var data = new ModelA(new ModelB());
        var modelC = data.GetModelC();

        var foo = () => data.GetModelD();

        Utility.UtilityMethod1();
        Execute();
    }

    private void Execute()
    {
        // DEP_CALLS_EXTERNAL
        Console.WriteLine("ServiceC.Execute");

        var d = new Command(TheAction);

        int[] numbers = { 1, 2, 3, 4, 5 };
        var result = numbers.Where(IsLargerThanOne);
    }

    private bool IsLargerThanOne(int n)
    {
        return n > 1;
    }

    private void TheAction()
    {
    }
}

internal class Command(Action a)
{
}