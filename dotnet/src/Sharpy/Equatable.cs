namespace Sharpy
{
    public interface Equatable<T> : Hashable, IEquatable<T>
    {
        bool __Eq__(T other);

        bool __Ne__(T other);
    }
}
