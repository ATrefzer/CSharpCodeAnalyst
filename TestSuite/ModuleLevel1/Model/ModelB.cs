using ModuleLevel2;

namespace ModuleLevel1.Model;

internal class ModelB
{
    private ModelC _modelC;
    private StructA _structA;

    public int Value { get; internal set; } = 10;

    internal void Initialize(ModelC modelC, StructA structA, ModelD[] arrayOfD)
    {
        _structA = new StructA();
        _structA.Fill(this);
        var theEnu = TheEnum.B;

        modelC.RecursiveFuncOnModelC();
    }

    public void Do()
    {
        _modelC.MethodOnModelC(this, TheEnum.A);

        var lamd = () => _modelC.MethodOnModelCCalledFromLambda(new[] { 3 });
        lamd();
    }
}