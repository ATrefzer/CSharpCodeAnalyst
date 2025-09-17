namespace Cycles;

internal class OuterClass
{
    private class DirectChildClass
    {
        private MiddleClass.NestedInnerClass x;
    }

    private class MiddleClass
    {
        public class NestedInnerClass
        {
            private DirectChildClass x;
        }
    }
}