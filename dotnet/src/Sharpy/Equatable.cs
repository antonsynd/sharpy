namespace Sharpy
{
    public interface Equatable<T> : IEquatable<T>
    {
        bool __Eq__(T other);
    }
}
