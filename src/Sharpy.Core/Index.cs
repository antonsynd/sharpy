namespace Sharpy.Core;

internal static class Index
{
    /// <summary>
    /// Returns a normalized index.
    /// </summary>
    /// <param name="i">The index to normalize.</param>
    /// <param name="max">The maximum length of the given sequence</param>
    /// <param name="forSlice">Whether this is for a slice or not.</param>
    /// <param name="forInsertion">Whether this is for insertion or not (allowing for past the end indices).</param>
    /// <returns>The normalized index.</returns>
    /// <exception cref="IndexError">If the given index is inherently out of range.</exception>
    internal static int Normalize(int i, int max, bool forSlice, bool forInsertion)
    {
        if (forSlice || forInsertion)
        {
            if (i < 0)
            {
                i = max + i;
            }

            return System.Math.Clamp(i, 0, max);
        }

        if (i >= max || i < -max)
        {
            throw new IndexError($"list index {i} out of range");
        }

        if (i < 0)
        {
            return max + i;
        }

        return i;
    }
}
