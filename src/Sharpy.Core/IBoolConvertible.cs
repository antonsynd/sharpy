namespace Sharpy
{
    /// <summary>
    /// Implemented by types that define __bool__() in Sharpy.
    /// Provides truthiness conversion for bool() dispatch.
    /// The compiler generates a __Bool__() method for types with __bool__,
    /// along with operator true/false for C# conditional expressions.
    /// </summary>
    public interface IBoolConvertible
    {
        bool __Bool__();
    }
}
