using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cycles
{
    internal class OuterClass
    {
        class DirectChildClass
        {
            MiddleClass.NestedInnerClass x;
        }

        class MiddleClass
        {
            public class NestedInnerClass
            {
                DirectChildClass x;
            }
        }
    }
}
