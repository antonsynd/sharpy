namespace Sharpy.Core;

using Operator;

public sealed partial class List<T>
{
    /// <summary>
    /// Implements the __lt__ dunder method for lexicographical comparison.
    /// Returns true if this list is lexicographically less than the other list.
    /// </summary>
    /// <param name="other">The list to compare with.</param>
    /// <returns>True if this list is less than the other list.</returns>
    public bool __Lt__(List<T> other)
    {
        if (other is null)
        {
            throw TypeError.OpNotSupported("<", "NoneType");
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
                return Operator.Exports.Lt(leftElem, rightElem);
            }
        }

        // If all compared elements are equal, the shorter list is less
        return _list.Count < other._list.Count;
    }
}
