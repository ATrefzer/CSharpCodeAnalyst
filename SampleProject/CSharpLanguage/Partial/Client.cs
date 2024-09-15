using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpLanguage.Partial
{
    internal class Client
    {
        public static Client CreateInstance()
        {
            var p = new PartialClass();
            p.MethodInPartialClassPart1();
            p.MethodInPartialClassPart2();
            return new Client();
        }
    }
}
