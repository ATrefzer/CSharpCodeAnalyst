using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Core.BasicLanguageFeatures
{
    internal class Lambdas
    {
        void Start()
        {
            var x = () =>
            {
                // Start -> uses -> CreatableClass
                var creatableClass = new CreatableClass();

                // Not extracted
                creatableClass.Nop();
            };

        }
    }
}
