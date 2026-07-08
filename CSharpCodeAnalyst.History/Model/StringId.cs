using System.Diagnostics;

namespace CSharpCodeAnalyst.History.Model
{
    [Serializable]
    public sealed class StringId : Id
    {
        private readonly string _id;

        public StringId(string id)
        {
            Debug.Assert(id != null);
            _id = id;
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

            return Equals((StringId) obj);
        }

        public override int GetHashCode()
        {
            return _id != null ? _id.GetHashCode() : 0;
        }

        public override string ToString()
        {
            return _id;
        }

        private bool Equals(StringId other)
        {
            return string.Equals(_id, other._id);
        }
    }
}