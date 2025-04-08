namespace Sharpy
{
    /// <summary>
    /// This interface defines objects that can be checked for equality via
    /// the <c>__Eq__</c> method. Equality in Sharpy is not the same as
    /// reference equality (except for <see cref="Object"/>). All Sharpy
    /// objects implement this interface. This is the non-generic version of
    /// the <see cref="Equatable&lt;T&gt;"/> interface.
    /// </summary>
    public interface Equatable : Hashable
    {
        /// <remarks>
        /// This should delegate to <see cref="__Eq__(object)"/>. All C# (and
        /// therefore Sharpy) objects declare this by existing because they
        /// all subclass <see cref="object"/> which implements this.
        /// </remarks>
        bool Equals(object? other);

        /// <remarks>
        /// This should delegate to <see cref="Equatable&lt;T&gt;.__Eq__(T)"/>
        /// when possible.
        /// </remarks>
        bool __Eq__(object other);
    }

    /// <summary>
    /// This interface defines objects that can be checked for equality via
    /// the <c>__Eq__</c> method. All Sharpy objects implement this interface.
    /// This is the generic version of the <see cref="Equatable"/> interface.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface Equatable<T> : Equatable, IEquatable<T>
    {
        /// <remarks>
        /// This defines equality between two objects of the same type.
        /// </remarks>
        bool __Eq__(T other);
    }
}
