namespace Sharpy;

public interface IGreaterThanOrEquatable<T> : IGreaterThanComparable<T>, IEquatable<T>
{
    bool __Ge__(T other);
}
