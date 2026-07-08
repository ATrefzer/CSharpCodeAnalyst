namespace CSharpCodeAnalyst.History.Model
{
    public sealed class NumberId : Id
    {
        public ulong Value { get; }

        public NumberId(ulong id)
        {
            Value = id;
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

            return Equals((NumberId) obj);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        private bool Equals(NumberId other)
        {
            return Value == other.Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}