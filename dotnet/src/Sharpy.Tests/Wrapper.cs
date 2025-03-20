namespace Sharpy.Tests
{
    public class Wrapper<T>(T value)
    {
        private static uint _id;

        public readonly uint Id = _id++;
        public readonly T Value = value;

        // public bool Equals(Wrapper<T> other)
        // {
        //     if (Value is null) {
        //         return other.Value is null && Id == other.Id;
        //     }

        //     return Value.Equals(other.Value);
        // }
    }
}
