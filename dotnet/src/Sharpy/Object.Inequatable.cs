namespace Sharpy
{
    public abstract partial class Object
    {
        /// <remarks>
        /// By default, inverts the result of <see cref="__Eq__(Object)"/>.
        /// </remarks>
        public virtual bool __Ne__(Object other)
        {
            return !__Eq__(other);
        }

        /// <inheritdoc/>
        public virtual bool __Ne__(object other)
        {
            return !__Eq__(other);
        }
    }
}
