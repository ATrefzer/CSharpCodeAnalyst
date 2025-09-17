namespace ModuleLevel1.Model;

internal class ModelA
{
    private readonly ModelB _modelB;

    private readonly ModelC _modelC;

    public ModelA(ModelB m)
    {
        _modelB = m;

        _modelC = new ModelC();

        // Ok, this is just treated as a call to the property.
        // Inside the ctor.
        var localFunc = () => _modelB.Value;

        var arrayOfD = new[] { new ModelD(), new ModelD(), new ModelD() };
        _modelB.Initialize(_modelC, new StructA(), arrayOfD);
    }

    public ModelC ModelCPropertyOfModelA
    {
        get
        {
            var x = _modelC.IntPropertyOfModelC;
            return _modelC;
        }
        set => throw new NotImplementedException();
    }

    private void AccessToPropertiesSetter()
    {
        ModelCPropertyOfModelA = new ModelC();
    }

    private void AccessToPropertiesGetter()
    {
        var modelC = ModelCPropertyOfModelA;
    }

    public ModelC GetModelC()
    {
        return _modelC;
    }

    public ModelD GetModelD()
    {
        return new ModelD();
    }
}