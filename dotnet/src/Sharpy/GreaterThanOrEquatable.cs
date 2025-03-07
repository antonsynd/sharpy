namespace Sharpy
{
    public interface GreaterThanOrEquatable<T> : GreaterThanComparable<T>, Equatable<T>, IComparable<T>
    {
        bool __Ge__(T other);
    }
}
