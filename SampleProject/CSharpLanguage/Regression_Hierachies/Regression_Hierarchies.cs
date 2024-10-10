using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpLanguage.Regression_Hierachies
{
    interface InterfaceA
    {
        void MethodA();
    }

    interface InterfaceB 
    {
        void MethodB();
    }

    interface InterfaceC
    {
        void MethodC();
    }

    class ClassBase : InterfaceA
    {
        virtual public void MethodA()
        {
            throw new NotImplementedException();
        }
    }

    abstract class ClassDerived1 : ClassBase, InterfaceB, InterfaceC
    {
        virtual public void MethodB()
        {
            throw new NotImplementedException();
        }

        public abstract void MethodC();
    }

    class ClassDerived2 : ClassDerived1
    {
        public override void MethodA()
        {
            base.MethodA();
        }

        public override void MethodC()
        {
            throw new NotImplementedException();
        }
    }

    class ClassDerived3 : ClassDerived2
    {
        override public void MethodB()
        {
            throw new NotImplementedException();
        }
    }

    class ClassDerived4 : ClassDerived3
    {
        public override void MethodA()
        {
            base.MethodA();
        }
    }
}
