using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModuleLevel1
{

    public interface IServiceC
    {
        void Do(int v);

        int IfProperty { get; set; }
    }
}
