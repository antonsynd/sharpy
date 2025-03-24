namespace Sharpy
{
    public interface LessThanOrEquatable<T> : LessThanComparable<T>, Equatable<T>
    {
        bool __Le__(T other);
    }
}
