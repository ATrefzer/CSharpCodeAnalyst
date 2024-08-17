using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpLanguage
{
    internal class ClassUsingAnEvent
    {
        void Init()
        {
            var x = new ClassOfferingAnEvent();
            x.MyEvent1 += MyEventHandler;

            x.MyEvent2 += MyEventHandler2;

            
        }

        private void MyEventHandler2(object? sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private void MyEventHandler(object sender, MyEventArgs e)
        {
            throw new CustomException();
        }
    }
}
