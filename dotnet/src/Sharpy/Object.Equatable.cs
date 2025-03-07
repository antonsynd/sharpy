namespace Sharpy
{
    public abstract partial class Object
    {
        /// <remarks>
        /// By default, returns whether this and other refer to the same object
        /// via <see cref="object.ReferenceEquals(object)"/> .
        /// </remarks>
        public virtual bool __Eq__(Object other)
        {
            // Default to reference equality
            return ReferenceEquals(this, other);
        }
    }
}
