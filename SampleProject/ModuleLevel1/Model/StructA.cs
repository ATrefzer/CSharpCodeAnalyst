using ModuleLevel2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModuleLevel1.Model
{
    public struct StructA
    {
        int _value = 0;

        int DependencyToConstant => Constants.Constant1;

        public StructA()
        {
        }

        internal void Fill(ModelB modelB)
        {
            _value = modelB.Value;
        }
    }
}
