namespace Sharpy;

/// <summary>
/// This interface defines hashable objects whose hashes can be obtained
/// via the <c>__Hash__</c> method.
/// </summary>
public interface IHashable
{
    /// <summary>
    /// This method should return the hash of the object. In Sharpy,
    /// objects that have the same hash are not guaranteed to be equal.
    /// </summary>
    /// <returns>The hash of the object.</returns>
    int __Hash__();
}
