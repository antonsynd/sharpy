using Sharpy.Core;

namespace Sharpy.Collections.Interfaces;

/// <summary>
/// Interface for mutable sequences.
/// </summary>
public interface IMutableSequence<T>
    : ISequence<T>,
      IInplaceAddable<ISequence<T>>
{
    /// <summary>
    /// Returns or sets the element at the given index. Supports negative
    /// indices to get or set from the back. If the index is out of range,
    /// then this raises an <see cref="IndexError"/>.
    /// </summary>
    new T this[int index]
    {
        get
        {
            return __GetItem__(Sharpy.Core.Index.Normalize(index, __Len__(), false, false));
        }
        set
        {
            __SetItem__(Sharpy.Core.Index.Normalize(index, __Len__(), false, false), value);
        }
    }

    new T this[System.Index index]
    {
        get
        {
            return __GetItem__(index.ToNormalizedInt32(__Len__(), false, false));
        }
        set
        {
            __SetItem__(index.ToNormalizedInt32(__Len__(), false, true), value);
        }
    }

    /// <summary>
    /// Returns the element at the given index. Supports negative indices
    /// to get or set from the back. If the index is out of range, then
    /// this raises an <see cref="IndexError"/>.
    /// </summary>
    new IMutableSequence<T> this[int start, int end]
    {
        get
        {
            return (IMutableSequence<T>)__GetItem__(new Slice(start, end));
        }
        set
        {
            __SetItem__(new Slice(start, end), value);
        }
    }

    new IMutableSequence<T> this[System.Range range]
    {
        get
        {
            return (IMutableSequence<T>)__GetItem__(range.ToSlice(__Len__(), false));
        }
        set
        {
            __SetItem__(range.ToSlice(__Len__(), true), value);
        }
    }

    /// <see cref="this[int, int]"/>
    new IMutableSequence<T> this[int start, int end, int step]
    {
        get
        {
            return (IMutableSequence<T>)__GetItem__(new Slice(start, end, step));
        }
        set
        {
            __SetItem__(new Slice(start, end, step), value);
        }
    }

    new IMutableSequence<T> this[Slice slice]
    {
        get
        {
            return (IMutableSequence<T>)__GetItem__(slice);
        }
        set
        {
            __SetItem__(slice, value);
        }
    }

    void Append(T x);

    /// <remarks>
    /// Iterable&lt;T&gt; extends IEnumerable&lt;T&gt;.
    /// </remarks>
    void Extend(IEnumerable<T> enumerable);

    void Insert(int i, T x);

    T Pop(int i = -1);

    new void Remove(T x);

    void Reverse();

    void __DelItem__(int i);

    void __DelItem__(Slice slice);

    void __SetItem__(int i, T x);

    void __SetItem__(Slice slice, ISequence<T> other);
}

public interface IMutableSequence<TSequence, TItem>
    : IMutableSequence<TItem>,
      ISequence<TSequence, TItem>
{
    new TSequence this[int start, int end]
    {
        get
        {
            return __GetItem__(new Slice(start, end));
        }
        set
        {
            __SetItem__(new Slice(start, end), value);
        }
    }

    new TSequence this[System.Range range]
    {
        get
        {
            return __GetItem__(range.ToSlice(__Len__(), false));
        }
        set
        {
            __SetItem__(range.ToSlice(__Len__(), true), value);
        }
    }

    /// <see cref="this[int, int]"/>
    new TSequence this[int start, int end, int step]
    {
        get
        {
            return __GetItem__(new Slice(start, end, step));
        }
        set
        {
            __SetItem__(new Slice(start, end, step), value);
        }
    }

    new TSequence this[Slice slice]
    {
        get
        {
            return __GetItem__(slice);
        }
        set
        {
            __SetItem__(slice, value);
        }
    }

    /// <inheritdoc/>
    void __SetItem__(Slice slice, TSequence other)
    {
        __SetItem__(slice, (IMutableSequence<TItem>)other);
    }
}
