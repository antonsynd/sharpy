namespace Sharpy.Core;

using System.Collections;
using Collections.Interfaces;

/// <summary>
/// View of dictionary items as (key, value) tuples.
/// This view reflects changes to the underlying dictionary.
/// </summary>
public sealed class DictItemsView<K, V> : IItemsView<K, V> where K : notnull
{
    private readonly Dictionary<K, V> _dict;

    internal DictItemsView(Dictionary<K, V> dict)
    {
        _dict = dict;
    }

    public bool Contains((K, V) item)
    {
        return __Contains__(item);
    }

    public IEnumerator<(K, V)> GetEnumerator()
    {
        foreach (var kvp in _dict)
        {
            yield return (kvp.Key, kvp.Value);
        }
    }

    public bool __Contains__((K, V) item)
    {
        if (_dict.TryGetValue(item.Item1, out V? value))
        {
            // Use Operator.Eq for proper equality comparison
            return Operator.Exports.Eq(value, item.Item2);
        }
        return false;
    }

    public Iterator<(K, V)> __Iter__()
    {
        return new EnumeratorIterator<(K, V)>(GetEnumerator());
    }

    public uint __Len__()
    {
        return (uint)_dict.Count;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
