using System;
using System.Collections.Generic;

namespace Sharpy
{
    public sealed partial class Dict<K, V> : IStrKeyDictionary
    {
        /// <summary>
        /// Projects this dictionary's entries as string-keyed pairs for
        /// reflection-free JSON dispatch. Returns an empty sequence when
        /// <typeparamref name="K"/> is not <see cref="string"/>.
        /// </summary>
        public IEnumerable<KeyValuePair<string, object?>> GetStringKeyEntries()
        {
            if (typeof(K) != typeof(string))
            {
                return Array.Empty<KeyValuePair<string, object?>>();
            }

            return EnumerateStringKeyEntries();
        }

        private IEnumerable<KeyValuePair<string, object?>> EnumerateStringKeyEntries()
        {
            foreach (var kvp in _dict)
            {
                var key = (string)(object)kvp.Key!;
                yield return new KeyValuePair<string, object?>(key, kvp.Value);
            }
        }
    }
}
