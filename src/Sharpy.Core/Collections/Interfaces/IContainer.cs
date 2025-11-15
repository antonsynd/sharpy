using Sharpy.Core;

namespace Sharpy.Collections.Interfaces;

/// <summary>
/// Interface for classes that provide the <see cref="__Contains__(T)"/>
/// method.
/// </summary>
public interface IContainer<T>
{
    /// <summary>
    /// Called to implement membership test operators. Should return
    /// <c>true</c> if item is in self, <c>false</c> otherwise.
    /// </summary>
    /// <remarks>
    /// For mapping objects, this should consider the keys of the mapping
    /// rather than the values or the key-item pairs.
    /// </remarks>
    bool __Contains__(T x);

    /// <remarks>
    /// In subclasses, this must call <see cref="__Contains__(T)" /> to
    /// correctly implement <c>x in y</c> behavior.
    /// </remarks>
    bool Contains(T x)
    {
        return __Contains__(x);
    }
}
