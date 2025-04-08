namespace Sharpy
{
    /// <summary>
    /// Base class for all Sharpy objects (except value types), deriving from
    /// C# object.
    /// </summary>
    public abstract partial class Object : object, Equatable<Object>, Inequatable<Object>, StrConvertible, BoolConvertible, Identifiable
    {
        /// <remarks>
        /// Not publicly constructible.
        /// </remarks>
        protected Object() { }
    }
}
