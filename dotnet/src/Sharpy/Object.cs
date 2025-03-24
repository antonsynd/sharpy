namespace Sharpy
{
    /// <summary>
    /// Base class for all Sharpy objects (except value types), deriving from
    /// C# object.
    /// </summary>
    public abstract partial class Object : object, Hashable, Equatable<Object>
    {
        /// <remarks>
        /// Not publicly constructible.
        /// </remarks>
        protected Object() { }
    }
}
