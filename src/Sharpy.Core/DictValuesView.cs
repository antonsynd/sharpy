namespace Sharpy.Core;

using System.Collections;
using Collections.Interfaces;

/// <summary>
/// View of dictionary values.
/// This view reflects changes to the underlying dictionary.
/// </summary>
public sealed class DictValuesView<K, V> : IValuesView<V> where K : notnull
{
    private readonly Dictionary<K, V>.ValueCollection _values;

    internal DictValuesView(Dictionary<K, V>.ValueCollection values)
    {
        _values = values;
    }

    public bool Contains(V item)
    {
        return __Contains__(item);
    }

    public IEnumerator<V> GetEnumerator()
    {
        foreach (var value in _values)
        {
            yield return value;
        }
    }

    public bool __Contains__(V item)
    {
        // Values don't have a fast Contains check in .NET, so we iterate
        foreach (var value in _values)
        {
            if (Operator.Exports.Eq(value, item))
            {
                return true;
            }
        }
        return false;
    }

    public Iterator<V> __Iter__()
    {
        return new EnumeratorIterator<V>(GetEnumerator());
    }

    public int __Len__()
    {
        return _values.Count;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
