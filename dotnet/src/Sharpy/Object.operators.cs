namespace Sharpy
{
    public abstract partial class Object
    {
        /// <remarks>
        /// Comparison between Objects is based on <see cref="__Eq__()"/>.
        /// </remarks>
        public static bool operator ==(Object? left, Object? right)
        {
            if (ReferenceEquals(left, right)) {
                return true;
            }

            if (left is null || right is null) {
                return false;
            }

            return left.__Eq__(right);
        }

        /// <remarks>
        /// Comparison between Objects is based on <see cref="__Eq__()"/>.
        /// </remarks>
        public static bool operator !=(Object? left, Object? right)
        {
            return !(left == right);
        }

        /// <remarks>
        /// Comparison between Sharpy Objects and C# objects is based on
        /// <see cref="Equals()"/> which is false if the C# object is not
        /// a type-erased Sharpy Object. If it is one, then it uses
        /// <see cref="__Eq__()"/>.
        /// </remarks>
        public static bool operator ==(Object? left, object? right)
        {
            if (left is null) {
                if (right is null) {
                    return true;
                }

                return false;
            }

            return left.Equals(right);
        }

        /// <remarks>
        /// Comparison between Sharpy Objects and C# objects is based on
        /// <see cref="Equals()"/> which is false if the C# object is not
        /// a type-erased Sharpy Object. If it is one, then it uses
        /// <see cref="__Eq__()"/>.
        /// </remarks>
        public static bool operator !=(Object? left, object? right)
        {
            return !(left == right);
        }

        /// <remarks>
        /// Symmetrical equality prioritizing Sharpy Object's
        /// <see cref="Equals()"/> implementation.
        /// </remarks>
        public static bool operator ==(object? left, Object? right)
        {
            if (right is null) {
                if (left is null) {
                    return true;
                }

                return false;
            }

            return right.Equals(left);
        }

        /// <remarks>
        /// Symmetrical equality prioritizing Sharpy Object's
        /// <see cref="Equals()"/> implementation.
        /// </remarks>
        public static bool operator !=(object? left, Object? right)
        {
            return !(left == right);
        }

        /// <remarks>
        /// Objects are truthy based on <see cref="__Bool__()"/>.
        /// </remarks>
        public static bool operator true(Object? obj)
        {
            return obj?.__Bool__() ?? false;
        }

        /// <remarks>
        /// Objects are falsey based on the inverse of
        /// <see cref="__Bool__()"/>.
        /// </remarks>
        public static bool operator false(Object? obj)
        {
            return !(obj?.__Bool__() ?? false);
        }
    }
}
