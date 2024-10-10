using ModuleLevel0;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModuleLevel2
{
    public class InterfaceImplementerInDifferentCompilation : InterfaceInDifferentCompilation
    {

        public event EventHandler AEvent;
   

        public void Method()
        {
            throw new NotImplementedException();
        }
    }
}
