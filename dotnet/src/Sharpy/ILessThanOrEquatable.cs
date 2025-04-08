namespace Sharpy
{
    public interface ILessThanOrEquatable<T> : ILessThanComparable<T>, IEquatable<T>
    {
        bool __Le__(T other);
    }
}
