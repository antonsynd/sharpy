namespace Sharpy
{
    public partial class Object
    {
        /// <remarks>
        /// Not virtual to prevent subclasses from overriding this.
        /// </remarks>
        public bool Equals(Object? obj)
        {
            return __Eq__(obj);
        }
    }
}
