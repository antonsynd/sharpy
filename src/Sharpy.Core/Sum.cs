namespace Sharpy.Core;

public static partial class Exports
{
    /// <summary>
    /// Sums a sequence of integers.
    /// </summary>
    public static int Sum(IEnumerable<int> iterable)
    {
        if (iterable is null)
        {
            throw TypeError.ArgNone("sum", "iterable");
        }

        return iterable.Sum();
    }

    /// <summary>
    /// Sums a sequence of longs.
    /// </summary>
    public static long Sum(IEnumerable<long> iterable)
    {
        if (iterable is null)
        {
            throw TypeError.ArgNone("sum", "iterable");
        }

        return iterable.Sum();
    }

    /// <summary>
    /// Sums a sequence of floats.
    /// </summary>
    public static float Sum(IEnumerable<float> iterable)
    {
        if (iterable is null)
        {
            throw TypeError.ArgNone("sum", "iterable");
        }

        return iterable.Sum();
    }

    /// <summary>
    /// Sums a sequence of doubles.
    /// </summary>
    public static double Sum(IEnumerable<double> iterable)
    {
        if (iterable is null)
        {
            throw TypeError.ArgNone("sum", "iterable");
        }

        return iterable.Sum();
    }

    /// <summary>
    /// Sums a sequence of decimals.
    /// </summary>
    public static decimal Sum(IEnumerable<decimal> iterable)
    {
        if (iterable is null)
        {
            throw TypeError.ArgNone("sum", "iterable");
        }

        return iterable.Sum();
    }
}
