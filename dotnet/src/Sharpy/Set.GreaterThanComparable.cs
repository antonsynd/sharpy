namespace Sharpy
{
    public sealed partial class Set<T>
    {
        public bool __Gt__(Set<T> other)
        {
            return !__Eq__(other) && !__Lt__(other);
        }

        public bool __Gt__(Collections.Interfaces.Set<T> other)
        {
            return !__Eq__(other) && !__Lt__(other);
        }
    }
}
