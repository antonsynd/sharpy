namespace Sharpy
{
    public abstract partial class Object
    {
        /// <remarks>
        /// By default, returns whether this and other refer to the same object
        /// via <see cref="object.ReferenceEquals()"/> with a fallback to
        /// <see cref="__Id__()"/>.
        /// </remarks>
        public virtual bool __Eq__(Object? other)
        {
            if (other is null) {
                return false;
            }

            return ReferenceEquals(this, other) || __Id__() == other.__Id__();
        }
    }
}
