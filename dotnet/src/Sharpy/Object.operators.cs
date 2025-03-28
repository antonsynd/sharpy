namespace Sharpy
{
    public abstract partial class Object
    {
        /// <remarks>
        /// Comparison between Objects is based on <see cref="__Eq__()"/>.
        /// </remarks>
        public static bool operator ==(Object left, Object right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            return left?.__Eq__(right) ?? right is null;
        }

        /// <remarks>
        /// Comparison between Objects is based on <see cref="__Eq__()"/>.
        /// </remarks>
        public static bool operator !=(Object left, Object right)
        {
            return !(left == right);
        }

        /// <remarks>
        /// Comparison between Sharpy Objects and C# objects is based on
        /// <see cref="Equals()"/> which is false if the C# object is not
        /// a type-erased Sharpy Object. If it is one, then it uses
        /// <see cref="__Eq__()"/>.
        /// </remarks>
        public static bool operator ==(Object left, object right)
        {
            return left?.Equals(right) ?? right is null;
        }

        /// <remarks>
        /// Comparison between Sharpy Objects and C# objects is based on
        /// <see cref="Equals()"/> which is false if the C# object is not
        /// a type-erased Sharpy Object. If it is one, then it uses
        /// <see cref="__Eq__()"/>.
        /// </remarks>
        public static bool operator !=(Object left, object right)
        {
            return !(left == right);
        }

        /// <remarks>
        /// Symmetrical equality prioritizing Sharpy Object's
        /// <see cref="Equals()"/> implementation.
        /// </remarks>
        public static bool operator ==(object left, Object right)
        {
            return right?.Equals(left) ?? left is null;
        }

        /// <remarks>
        /// Symmetrical equality prioritizing Sharpy Object's
        /// <see cref="Equals()"/> implementation.
        /// </remarks>
        public static bool operator !=(object left, Object right)
        {
            return !(left == right);
        }

        /// <remarks>
        /// Objects are truthy based on <see cref="__Bool__()"/>.
        /// </remarks>
        public static bool operator true(Object obj)
        {
            return obj?.__Bool__() ?? false;
        }

        /// <remarks>
        /// Objects are falsey based on the inverse of
        /// <see cref="__Bool__()"/>.
        /// </remarks>
        public static bool operator false(Object obj)
        {
            return !(obj?.__Bool__() ?? false);
        }
    }
}
