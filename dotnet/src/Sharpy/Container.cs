namespace Sharpy
{
    public interface Container<T>
    {
        bool __Contains__(T x);

        /// <remarks>
        /// In subclasses, this must call __Contains__(x) to correctly implement
        /// `x in y` behavior.
        /// </remarks>
        bool Contains(T x);
    }
}
