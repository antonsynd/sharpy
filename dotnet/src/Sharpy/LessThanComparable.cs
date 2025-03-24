namespace Sharpy
{
    public interface LessThanComparable<T> : IComparable<T>
    {
        bool __Lt__(T? other);
    }
}
