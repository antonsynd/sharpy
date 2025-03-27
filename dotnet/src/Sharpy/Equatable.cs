namespace Sharpy
{
    public interface Equatable<T> : Hashable, IEquatable<T>
    {
        /// <remarks>
        /// This should delegate to <see cref="__Eq__(object)"/>. All C# (and
        /// therefore Sharpy) objects declare this by existing because they
        /// all subclass <see cref="object"/> which implements this.
        /// </remarks>
        bool Equals(object? other);

        /// <remarks>
        /// This should delegate to <see cref="__Eq__(T)"/> when possible.
        /// </remarks>
        bool __Eq__(object other);

        bool __Eq__(T other);
    }
}
