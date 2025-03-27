namespace Sharpy
{
    public abstract partial class Object
    {
        /// <remarks>
        /// By default, returns whether this and other refer to the same object
        /// via <see cref="object.ReferenceEquals()"/> with a fallback to
        /// <see cref="__Id__()"/>.
        /// </remarks>
        public virtual bool __Eq__(Object other)
        {
            if (other is null)
            {
                return false;
            }

            return ReferenceEquals(this, other) || __Id__() == other.__Id__();
        }

        public virtual bool __Eq__(object other)
        {
            if (other is Object obj)
            {
                return __Eq__(obj);
            }

            // NOTE: Do NOT call Equals() because it will result in an infinite
            // loop as Equals() ultimately references __Eq__()
            return ReferenceEquals(this, other);
        }
    }
}
