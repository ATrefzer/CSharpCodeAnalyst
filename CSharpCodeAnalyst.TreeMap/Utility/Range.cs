namespace CSharpCodeAnalyst.TreeMap.Utility
{
    public sealed class Range<T> where T : IComparable<T>
    {
        public Range(T min, T max)
        {
            Max = max;
            Min = min;
        }

        public T Max { get; }
        public T Min { get; }

        public bool Contains(T value)
        {
            return value.CompareTo(Min) >= 0 &&
                   value.CompareTo(Max) <= 0;
        }

        public bool Contains(params T[] values)
        {
            foreach (var value in values)
            {
                if (!Contains(value))
                {
                    return false;
                }
            }

            return true;
        }
    }
}