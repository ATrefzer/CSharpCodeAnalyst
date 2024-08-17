using Microsoft.VisualBasic;
using ModuleLevel2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModuleLevel1.Model
{
    internal class ModelB
    {
        private ModelC _modelC;
        private StructA _structA;

        public int Value { get; internal set; } = 10;

        internal void Initialize(ModelC modelC, StructA structA, ModelD[] arrayOfD)
        {
            _structA = new StructA();
            _structA.Fill(this);
            TheEnum theEnu = TheEnum.B;

            modelC.RecursiveFuncOnModelC();
        }

        public void Do()
        {
            _modelC.MethodOnModelC(this, TheEnum.A);

            var lamd = () => _modelC.MethodOnModelCCalledFromLambda(new int[] {3});
            lamd();
        }

    }
}
