namespace Sharpy
{
    public interface Inequatable<T> : IEquatable<T>
    {
        bool __Ne__(T other);

        bool __Ne__(object other);
    }
}
