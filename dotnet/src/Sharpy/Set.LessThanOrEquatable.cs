namespace Sharpy
{
    public sealed partial class Set<T>
    {
        public bool __Le__(Set<T> other)
        {
            return __Eq__(other) || __Lt__(other);
        }


        public bool __Le__(Collections.Interfaces.Set<T> other)
        {
            return __Eq__(other) || __Lt__(other);
        }
    }
}
