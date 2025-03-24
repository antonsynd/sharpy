namespace Sharpy
{
    public interface GreaterThanComparable<T> : IComparable<T>
    {
        bool __Gt__(T? other);
    }
}
