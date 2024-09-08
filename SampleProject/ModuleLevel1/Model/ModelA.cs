namespace ModuleLevel1.Model
{
    internal class ModelA
    {
        private ModelB _modelB;

        private ModelC _modelC;

        public ModelA(ModelB m)
        {
            _modelB = m;

            _modelC = new ModelC();

            // Ok, this is just treated as a call to the property.
            // Inside the ctor.
            var localFunc = () => _modelB.Value;

            var arrayOfD = new ModelD[] { new ModelD(), new ModelD(), new ModelD() };
            _modelB.Initialize(_modelC, new StructA(), arrayOfD);
        }

        void AccessToPropertiesSetter()
        {
            ModelCPropertyOfModelA = new ModelC();
        }

        void AccessToPropertiesGetter()
        {
            var modelC = ModelCPropertyOfModelA;
        }

        public ModelC ModelCPropertyOfModelA
        {
            get
            {
                int x = _modelC.IntPropertyOfModelC;
                return _modelC;
            }
            set => throw new NotImplementedException();
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
}
