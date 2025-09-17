using ModuleLevel2;

namespace ModuleLevel1.Model;

public class ModelC
{

    public int IntPropertyOfModelC
    {
        get => RecursiveFuncOnModelC();
    }

    public int RecursiveFuncOnModelC()
    {
        return RecursiveFuncOnModelC();
    }

    internal void MethodOnModelC(ModelB modelB, TheEnum a)
    {
        throw new NotImplementedException();
    }

    internal object MethodOnModelCCalledFromLambda(int[] ints)
    {
        var enumList = new List<TheEnum>();
        throw new NotImplementedException();
    }
}