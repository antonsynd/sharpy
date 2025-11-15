namespace Sharpy.Core;

using Operator;

public sealed partial class List<T> : IGreaterThanComparable<List<T>>
{
    /// <summary>
    /// Implements the __gt__ dunder method for lexicographical comparison.
    /// Returns true if this list is lexicographically greater than the other list.
    /// </summary>
    /// <param name="other">The list to compare with.</param>
    /// <returns>True if this list is greater than the other list.</returns>
    public bool __Gt__(List<T> other)
    {
        if (other is null)
        {
            throw TypeError.OpNotSupported(">", "NoneType");
        }

        // Lexicographical comparison
        var minLen = Math.Min(_list.Count, other._list.Count);

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

        // If all compared elements are equal, the longer list is greater
        return _list.Count > other._list.Count;
    }
}
