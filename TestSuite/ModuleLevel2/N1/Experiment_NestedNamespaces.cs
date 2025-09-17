using Insight.Dialogs;
using ModuleLevel2.N1.N2.N3;

internal class ClassInGlobalNs
{
}

namespace ModuleLevel2.N1
{
    namespace N2
    {

        namespace N3
        {
            internal class ClassInNs2
            {
                private ClassInNs1 _y;
            }
        }
    }

    internal class ClassInNs1
    {
        private ClassInNs2 _x;
    }


}


namespace A.B.C
{
    // Is a nested namespace A, B, C
}


namespace Insight
{
    internal class Analyzers
    {
        private TrendViewModel _vm;
    }
}

namespace Insight.Dialogs
{
    internal class TrendViewModel
    {
        private Analyzers _analyzer;
    }
}

//namespace ModuleLevel2.NS1.NS2.NS3
//{
//    internal class ClassInNs2
//    {
//        private ClassInNs1 _y;
//    }
//}