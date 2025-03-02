namespace Sharpy
{
    /// <summary>
    /// Base class for all Sharpy objects (except value types), deriving from
    /// C# object.
    /// </summary>
    public abstract partial class Object : object, Hashable
    {
        /// <remarks>
        /// Not publicly constructible.
        /// </remarks>
        protected Object() { }

        public abstract bool __Bool__();

        /// <remarks>
        /// By default, calls <see cref="__Repr__()"/>.
        /// </remarks>
        public virtual string __Str__()
        {
            return __Repr__();
        }

        /// <summary>
        /// By default, returns a string containing the result of
        /// <see cref="__Id__()"/> in the form <c>"&lt;Object object with id {__Id__()}&gt;"</c>.
        /// </summary>
        /// <returns></returns>
        public virtual string __Repr__()
        {
            return $"<Object object with id {__Id__()}>";
        }

        /// <remarks>
        /// By default, returns whether this and other refer to the same object
        /// via <see cref="object.ReferenceEquals(object)"/> .
        /// </remarks>
        public virtual bool __Eq__(Object other)
        {
            // Default to reference equality
            return ReferenceEquals(this, other);
        }

        /// <remarks>
        /// By default, inverts the result of <see cref="__Eq__(Object)"/>.
        /// </remarks>
        public virtual bool __Ne__(Object other)
        {
            return !__Eq__(other);
        }

        /// <remarks>
        /// In Sharpy's reference implementation in C#, this returns the
        /// hashcode of the object by calling <see cref="object.GetHashCode()"/>
        /// by default (not by <see cref="__Hash__()"/>). This is because
        /// objects in C# are not guaranteed to have pinned memory addresses.
        /// </remarks>
        public virtual int __Id__()
        {
            return base.GetHashCode();
        }

        /// <remarks>
        /// By default, returns <see cref="object.GetHashCode()"/>.
        /// </remarks>
        public virtual int __Hash__()
        {
            return base.GetHashCode();
        }

        public static bool operator ==(Object left, Object right)
        {
            return left.__Eq__(right);
        }

        public static bool operator !=(Object left, Object right)
        {
            return !(left == right);
        }

        public static bool operator ==(Object left, object right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Object left, object right)
        {
            return !(left == right);
        }
    }
}
