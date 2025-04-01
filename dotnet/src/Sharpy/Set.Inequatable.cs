namespace Sharpy
{
    public sealed partial class Set<T>
    {
        public bool __Ne__(Set<T> other)
        {
            return __Eq__(other);
        }

        public bool __Ne__(Collections.Interfaces.Set<T> other)
        {
            return !__Eq__(other);
        }
    }
}
