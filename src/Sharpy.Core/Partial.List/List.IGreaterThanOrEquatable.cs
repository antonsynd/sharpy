using System.Linq;
namespace Sharpy.Core
{
    using Operator;

    public sealed partial class List<T>
    {
        /// <summary>
        /// Implements the __ge__ dunder method for lexicographical comparison.
        /// Returns true if this list is lexicographically greater than or equal to the other list.
        /// </summary>
        /// <param name="other">The list to compare with.</param>
        /// <returns>True if this list is greater than or equal to the other list.</returns>
        public bool __Ge__(List<T> other)
        {
            if (other is null)
            {
                throw TypeError.OpNotSupported(">=", "NoneType");
            }

            // Lexicographical comparison
            var minLen = System.Math.Min(_list.Count, other._list.Count);

            for (int i = 0; i < minLen; i++)
            {
                var leftElem = _list[i];
                var rightElem = other._list[i];

                // If elements are not equal, compare them
                if (!Operator.Exports.Eq(leftElem, rightElem))
                {
                    return Operator.Exports.Gt(leftElem, rightElem);
                }
            }

            // If all compared elements are equal, check if left is longer or equal
            return _list.Count >= other._list.Count;
        }
    }
}
