namespace Sharpy
{
    /// <summary>
    /// Implemented by types that define __bool__() in Sharpy.
    /// Provides truthiness conversion for bool() dispatch.
    /// The compiler generates an IsTrue property for types with __bool__,
    /// along with operator true/false for C# conditional expressions.
    /// </summary>
    public interface IBoolConvertible
    {
        /// <summary>Returns true if the object is considered truthy.</summary>
        bool IsTrue { get; }
    }
}
