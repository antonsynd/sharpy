using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// The single implementation of Python mapping equality shared by every <c>collections</c>
    /// mapping's <c>operator ==</c>/<c>!=</c> overloads (#1933). Python compares mappings by
    /// CONTENTS — same key set with equal values — for every mapping except <c>OrderedDict ==
    /// OrderedDict</c>, which is order-sensitive. Values are compared with <c>Operator.Eq</c>,
    /// exactly as <see cref="Dict{K, V}.Equals(Dict{K, V})"/> does, so nested Sharpy values use
    /// their own <c>__eq__</c>.
    /// </summary>
    internal static class MappingEquality
    {
        /// <summary>
        /// Order-insensitive content equality: the two mappings are equal iff they have the same
        /// number of pairs and every key of <paramref name="left"/> is present in
        /// <paramref name="right"/> with an equal value.
        /// </summary>
        public static bool PairwiseEquals<K, V>(
            int leftCount, IEnumerable<KeyValuePair<K, V>> left,
            int rightCount, IEnumerable<KeyValuePair<K, V>> right) where K : notnull
        {
            if (leftCount != rightCount)
            {
                return false;
            }

            var rightMap = new Dictionary<K, V>();
            foreach (var kv in right)
            {
                rightMap[kv.Key] = kv.Value;
            }

            foreach (var kv in left)
            {
                if (!rightMap.TryGetValue(kv.Key, out V? rightValue))
                {
                    return false;
                }

                if (!ContentEquals(kv.Value, rightValue))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Order-sensitive content equality: equal pairs in the same positions. Used only by
        /// <c>OrderedDict == OrderedDict</c>, which CPython makes order-sensitive.
        /// </summary>
        public static bool OrderedEquals<K, V>(
            IReadOnlyList<KeyValuePair<K, V>> left,
            IReadOnlyList<KeyValuePair<K, V>> right) where K : notnull
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                if (!ContentEquals(left[i].Key, right[i].Key))
                {
                    return false;
                }

                if (!ContentEquals(left[i].Value, right[i].Value))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ContentEquals(object? a, object? b)
        {
            if (a is null)
            {
                return b is null;
            }

            if (b is null)
            {
                return false;
            }

            return Operator.Eq(a, b);
        }
    }
}
