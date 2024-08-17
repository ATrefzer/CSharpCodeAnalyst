
using ModuleLevel2;

namespace ModuleLevel1.Model
{
    public class ModelC
    {

        public int RecursiveFuncOnModelC()
        {
            return RecursiveFuncOnModelC();
        }

        internal void MethodOnModelC(ModelB modelB, ModuleLevel2.TheEnum a)
        {
            throw new NotImplementedException();
        }

      public int IntPropertyOfModelC
        {
            get
            {
                return RecursiveFuncOnModelC();
            }
        }

        internal object MethodOnModelCCalledFromLambda(int[] ints)
        {
            var enumList = new List<TheEnum>();
            throw new NotImplementedException();
        }
    }
}