using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// Implemented by dictionary-like types that can project their entries as
    /// string-keyed pairs. Enables compile-time dispatch for JSON serialization
    /// without reflection.
    /// </summary>
    public interface IStrKeyDictionary
    {
        /// <summary>
        /// Enumerates the entries of this dictionary as string-keyed pairs.
        /// Implementations whose key type is not <see cref="string"/> should
        /// return an empty sequence.
        /// </summary>
        IEnumerable<KeyValuePair<string, object?>> GetStringKeyEntries();
    }
}
