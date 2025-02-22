namespace Sharpy
{
    /// <summary>
    /// Base class for all Sharpy objects (except value types), deriving from
    /// C# object.
    /// </summary>
    public class Object : object
    {
        /// <remarks>
        /// Not publicly constructible.
        /// </remarks>
        protected Object() { }

        public virtual string __Str__()
        {
            return __Repr__();
        }

        public virtual string __Repr__()
        {
            return $"<Object object with id {__Id__()}>";
        }

        public virtual bool __Eq__(Object other)
        {
            // Default to reference equality
            return ReferenceEquals(this, other);
        }

        public virtual bool __Ne__(Object other)
        {
            return !__Eq__(other);
        }

        public virtual int __Id__()
        {
            return base.GetHashCode();
        }

        public virtual int __Hash__()
        {
            return base.GetHashCode();
        }

        /// <remarks>
        /// Sealed to prevent subclasses from overriding this mapping to
        /// __Eq__() which should be the one that subclasses override.
        /// </remarks>
        public override sealed bool Equals(object? obj)
        {
            if (obj is Object other)
            {
                return __Eq__(other);
            }

            return false;
        }

        /// <remarks>
        /// Sealed to prevent subclasses from overriding this mapping to
        /// __Hash__() which should be the one that subclasses override.
        /// </remarks>
        public override sealed int GetHashCode()
        {
            return __Hash__();
        }

        /// <remarks>
        /// Sealed to prevent subclasses from overriding this mapping to
        /// __Str__() which should be the one that subclasses override.
        /// </remarks>
        public override sealed string ToString()
        {
            return __Str__();
        }
    }
}
