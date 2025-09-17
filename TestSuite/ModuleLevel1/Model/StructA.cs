using ModuleLevel2;

namespace ModuleLevel1.Model;

public struct StructA
{
    private int _value = 0;

    private int DependencyToConstant
    {
        get => Constants.Constant1;
    }

    public StructA()
    {
    }

    internal void Fill(ModelB modelB)
    {
        _value = modelB.Value;
    }
}