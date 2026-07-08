using System.Diagnostics;

namespace CSharpCodeAnalyst.History.Model
{
    public class OrderedPair
    {
        public OrderedPair(string item1, string item2)
        {
            Item1 = item1;
            Item2 = item2;

            Debug.Assert(Key() == Key(item1, item2));
            Debug.Assert(Key() == Key(item2, item1));
        }

        public string Item1 { get; }
        public string Item2 { get; }

        public static string Key(string item1, string item2)
        {
            var order = string.CompareOrdinal(item1, item2);

            if (order >= 0)
            {
                return string.Join("__", item1, item2);
            }

            return string.Join("__", item2, item1);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((OrderedPair) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Item1?.GetHashCode() ?? 0) * 397) ^
                       (Item2?.GetHashCode() ?? 0);
            }
        }

        private bool Equals(OrderedPair other)
        {
            return string.Equals(Item1, other.Item1) && string.Equals(Item2, other.Item2);
        }

        private string Key()
        {
            // Note the key is always the combined sorted entities!
            return Key(Item1, Item2);
        }
    }

    public class Coupling : OrderedPair
    {
        public Coupling(string item1, string item2) : base(item1, item2)
        {
        }

        public int Couplings { get; set; }
        public double Degree { get; set; }
    }

    public class Dependency
    {
    }
}