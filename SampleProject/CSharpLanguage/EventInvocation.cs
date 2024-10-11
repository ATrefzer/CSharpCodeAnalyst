using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace CSharpLanguage
{
    public class EventSink
    {
        public EventSink(IInterfaceWithEvent e)
        {
            e.MyEvent += Handler;
        }

        private void Handler(object? sender, EventArgs e)
        {
            throw new NotImplementedException();
        }
    }

    public interface IInterfaceWithEvent
    {
        event EventHandler<EventArgs> MyEvent;
    }
    internal class EventInvocation : IInterfaceWithEvent
    {
        public event EventHandler<EventArgs> MyEvent;

        public void Raise1()
        {
         
            MyEvent?.Invoke(this, EventArgs.Empty);
        }

        public void Raise2()
        {
            MyEvent.Invoke(this, EventArgs.Empty);
        }

        public void Raise3()
        {
            MyEvent(this, EventArgs.Empty);
        }

        public void DoSomething()
        {
            // Do
            Raise1();
            Raise2();
            Raise3();
        }
    }
}
