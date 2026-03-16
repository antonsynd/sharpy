using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// Implemented by types that support deep copying with circular reference tracking.
    /// Used by <see cref="CopyModule.Deepcopy"/> to avoid reflection-based deep copy
    /// for known collection types.
    /// </summary>
    public interface IDeepCopyable
    {
        /// <summary>
        /// Creates a deep copy of this object, using <paramref name="memo"/> to track
        /// already-copied objects and handle circular references.
        /// </summary>
        /// <param name="memo">
        /// An identity-based dictionary mapping original objects to their copies.
        /// Implementations must register themselves in the memo before copying children.
        /// </param>
        /// <returns>A deep copy of this object.</returns>
        object DeepCopy(Dictionary<object, object> memo);
    }
}
