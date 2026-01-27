namespace Sharpy.Core;

using Collections.Interfaces;

public sealed partial class List<T>
{
    /// <summary>
    /// Gets or sets the element at the specified index.
    /// </summary>
    /// <remarks>
    /// Supports negative indexing: -1 is the last element, -2 is second to last, etc.
    /// </remarks>
    public T this[int index]
    {
        get
        {
            index = Sharpy.Core.Index.Normalize(index, _list.Count, false, false);
            return _list[index];
        }
        set
        {
            index = Sharpy.Core.Index.Normalize(index, _list.Count, false, false);
            _list[index] = value;
        }
    }

    /// <inheritdoc/>
    public List<T> this[int start, int end]
    {
        get => __GetItem__(new Slice(start, end, 1));
        set => __SetItem__(new Slice(start, end, 1), value);
    }

    /// <inheritdoc/>
    public List<T> this[int start, int end, int step]
    {
        get => __GetItem__(new Slice(start, end, step));
        set => __SetItem__(new Slice(start, end, step), value);
    }

    IMutableSequence<T> IMutableSequence<T>.this[int start, int end]
    {
        get => this[start, end];
        set => this[start, end] = [.. value];
    }

    IMutableSequence<T> IMutableSequence<T>.this[int start, int end, int step]
    {
        get => this[start, end, step];
        set => this[start, end, step] = [.. value];
    }

    /// <summary>
    /// Add an item to the end of the list. Similar to a[len(a):] = [x].
    /// </summary>
    public void Append(T x)
    {
        _list.Add(x);
    }

    /// <summary>
    /// Extend the list by appending all the items from the iterable.
    /// Similar to a[len(a):] = iterable.
    /// </summary>
    public void Extend(IEnumerable<T> enumerable)
    {
        if (enumerable is null)
        {
            throw TypeError.ArgNone("extend", "enumerable");
        }

        _list.AddRange(enumerable);
    }

    /// <summary>
    /// Remove all items from the list.
    /// </summary>
    public void Clear()
    {
        _list.Clear();
    }

    /// <summary>
    /// Insert an item at a given position. The first argument is the
    /// index of the element before which to insert, so a.Insert(0, x)
    /// inserts at the front of the list, and a.Insert(Len(a), x) is
    /// equivalent to a.Append(x).
    /// </summary>
    public void Insert(int i, T x)
    {
        _list.Insert(Sharpy.Core.Index.Normalize(i, _list.Count, false, true), x);
    }

    /// <summary>
    /// Remove the item at the given position in the list, and return it.
    /// If no index is specified, a.Pop() removes and returns the last
    /// item in the list. It raises an IndexError if the list is empty or
    /// the index is outside the list range.
    /// </summary>
    public T Pop(int i = -1)
    {
        if (_list.Count == 0)
        {
            throw new IndexError("pop from empty list");
        }

        try
        {
            i = Sharpy.Core.Index.Normalize(i, _list.Count, false, false);
        }
        catch (IndexError)
        {
            throw new IndexError($"pop index {i} out of range");
        }

        var item = _list[i];
        _list.RemoveAt(i);

        return item;
    }

    /// <summary>
    /// Remove the first item from the list whose value is equal to x. It
    /// raises a ValueError if there is no such item.
    /// </summary>
    public void Remove(T x)
    {
        if (!_list.Remove(x))
        {
            throw new ValueError($"{x} not in list");
        }
    }

    /// <summary>
    /// Reverse the elements of the list in place.
    /// </summary>
    public void Reverse()
    {
        _list.Reverse();
    }

    /// <inheritdoc/>
    public void __IAdd__(ISequence<T> other)
    {
        if (other is null)
        {
            throw TypeError.IsNotInterface("NoneType", "iterable");
        }

        _list.AddRange(other);
    }

    /// <inheritdoc/>
    public void __SetItem__(Slice slice, ISequence<T> other)
    {
        if (other is null)
        {
            throw TypeError.IsNotInterface("NoneType", "iterable");
        }

        __SetItem__(slice, [.. other]);
    }

    ISequence<T> ISequence<T>.__GetItem__(Slice slice)
    {
        return __GetItem__(slice);
    }

    /// <inheritdoc/>
    public void __DelItem__(int index)
    {
        _list.RemoveAt(Sharpy.Core.Index.Normalize(index, _list.Count, false, false));
    }

    /// <inheritdoc/>
    public void __DelItem__(Slice slice)
    {
        if (slice.step == 0)
        {
            throw new ValueError("slice step cannot be zero");
        }

        if (slice.step < 0)
        {
            // Negative slice is no-op
            return;
        }

        (int start, int end) = Sharpy.Core.Slice.Normalize(slice.start, slice.end, _list.Count);

        if (slice.step == 1)
        {
            if (start == end)
            {
                // No-op
                return;
            }

            // Replace entire given range
            _DeleteSliceSingleStep(start, end);
        }
        else
        {
            _DeleteSliceMultiStep(start, end, slice.step);
        }
    }

    private void _DeleteSliceSingleStep(int start, int end)
    {
        _list.RemoveRange(start, end - start);
    }

    private void _DeleteSliceMultiStep(int start, int end, int step)
    {
        var numElemsToChange = Slice.Len(start, end, step);

        int elemsChanged = 0;

        for (int i = start; i < end && elemsChanged < numElemsToChange; ++i)
        {
            if ((i - start) % step == 0)
            {
                _list.RemoveAt(i - elemsChanged);

                ++elemsChanged;
            }
        }
    }

    /// <summary>
    /// Sets the element at the specified index.
    /// </summary>
    /// <remarks>
    /// Deprecated: Use the indexer <c>list[index] = value</c> instead.
    /// </remarks>
    public void __SetItem__(int index, T value)
    {
        this[index] = value;
    }

    /// <inheritdoc/>
    public void __SetItem__(Slice slice, List<T> other)
    {
        if (other is null)
        {
            throw TypeError.IsNotInterface("NoneType", "iterable");
        }

        if (slice.step == 0)
        {
            throw new ValueError("slice step cannot be zero");
        }

        if (slice.step < 0)
        {
            // Negative slice is no-op
            return;
        }

        (int start, int end) = Slice.Normalize(slice.start, slice.end, _list.Count);

        if (slice.step == 1)
        {
            if (start == end)
            {
                // Simple insertion
                _SetSliceInsertion(other, start);
            }
            else
            {
                // Replace entire given range
                _SetSliceSingleStep(other, start, end);
            }
        }
        else
        {
            _SetSliceMultiStep(other, start, end, slice.step);
        }
    }

    private void _SetSliceInsertion(List<T> other, int start)
    {
        if (other._list.Count == 0)
        {
            return;
        }

        _list.EnsureCapacity(_list.Count + other._list.Count);
        _list.InsertRange(start, other);
    }

    private void _SetSliceSingleStep(List<T> other, int start, int end)
    {
        var numOldElems = end - start;
        var numNewElems = other.__Len__();

        if (numOldElems != numNewElems)
        {
            _SetSliceSingleStepReplacement(other, start, numOldElems,
                                            numNewElems);
        }
        else
        {
            // Trivial case, replace 1-to-1
            for (int i = start; i < end; ++i)
            {
                _list[i] = other[i - start];
            }
        }
    }

    private void _SetSliceSingleStepReplacement(List<T> other,
                                        int start,
                                        int numOldElems,
                                        int numNewElems)
    {
        if (numNewElems > numOldElems)
        {
            var numExtraElems = numNewElems - numOldElems;
            _list.EnsureCapacity(_list.Count + numExtraElems);
        }

        // TODO: Can optimize for fewer element shifts
        _list.RemoveRange(start, numOldElems);
        _list.InsertRange(start, other);
    }

    private void _SetSliceMultiStep(List<T> other, int start, int end, int step)
    {
        var numElemsToChange = Slice.Len(start, end, step);

        if (other._list.Count != numElemsToChange)
        {
            throw new ValueError($"Attempt to assign sequence of size "
                + $"{other._list.Count} to extended slice of size "
                + $"{numElemsToChange}");
        }

        int elemsChanged = 0;

        for (int i = start; i < end && elemsChanged < numElemsToChange; ++i)
        {
            if ((i - start) % step == 0)
            {
                _list[i] = other[elemsChanged];

                ++elemsChanged;
            }
        }
    }
}
