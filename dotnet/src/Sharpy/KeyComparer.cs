namespace Sharpy;

/// <summary>
/// Delegator to statically chosen comparer.
/// </summary>
file class KeyComparer<T, TKey>(Func<T, TKey> key) : IComparer<T>
{
    private readonly Func<T, TKey> _key = key;

    /// <remarks>
    /// Unlike in Python, this compares None to non-None values as ordering
    /// before the non-None value, whereas Python disallows such comparisons
    /// by raising a TypeError.
    /// </remarks>
    public int Compare(T? x, T? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null || y is null)
        {
            throw new TypeError("'<' not supported for instances of 'NoneType'");
        }

        return ComparerAdapter<TKey>.Instance.Compare(_key(x), _key(y));
    }

}

/// <summary>
/// Simplifies creation of a key comparer if the key is null, by returning
/// the comparer for T rather than for TKey.
/// </summary>
internal class KeyComparerFactory<T, TKey>
{
    public static IComparer<T> Create(Func<T, TKey> key)
    {
        return new KeyComparer<T, TKey>(key);
    }
}
