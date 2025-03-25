namespace Sharpy.Tests
{
    public class Wrapper<T>(T value) : Object
    {
        private static uint _id;

        public readonly uint Id = _id++;
        public readonly T Value = value;

        public override bool __Eq__(Object other)
        {
            if (other is Wrapper<T> wrapper)
            {
                return Equals(Value, wrapper.Value);
            }

            return false;
        }

        public static implicit operator Wrapper<T>(T value)
        {
            return new Wrapper<T>(value);
        }

        public static bool operator ==(Wrapper<T> left, Wrapper<T> right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left is null || right is null)
            {
                return false;
            }

            return Equals(left.Value, right.Value);
        }

        public static bool operator !=(Wrapper<T> left, Wrapper<T> right)
        {
            return !(left == right);
        }

        public override bool __Bool__()
        {
            return Bool(Value);
        }
    }
}
