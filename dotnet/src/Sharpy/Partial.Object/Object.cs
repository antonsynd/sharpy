namespace Sharpy;

/// <summary>
/// Base class for all Sharpy objects (except value types), deriving from
/// C# object.
/// </summary>
public abstract partial class Object : object, IEquatable<Object>, IInequatable<Object>, IStrConvertible, IBoolConvertible, IIdentifiable
{
    /// <remarks>
    /// Not publicly constructible.
    /// </remarks>
    protected Object() { }
}
