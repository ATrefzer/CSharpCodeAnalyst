using CSharpLanguage.NS_Parent.NS_Child;

namespace CSharpLanguage.NS_Parent
{
    namespace NS_Irrelevant
    {
        internal class ClassNsIrrelevant
        {
            private ClassNsChild.DelegateInChild _delegate2;
        }
    }

    internal class ClassINparent
    {
        private ClassNsChild.DelegateInChild _delegate1;
    }


    namespace NS_Child
    {
        internal class ClassNsChild
        {
            public delegate void DelegateInChild();

            public void Method()
            {
            }
        }
    }
}