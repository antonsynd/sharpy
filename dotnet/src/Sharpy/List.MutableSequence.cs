namespace Sharpy
{
    public sealed partial class List<T>
    {
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
            _list.Insert((int)_NormalizeIndex(i, false, true), x);
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
                i = (int)_NormalizeIndex(i, false, false);
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


        public void __DelItem__()
        {
            _list.Clear();
        }

        public void __DelItem__(int index)
        {
            _list.RemoveAt((int)_NormalizeIndex(index, false, false));
        }

        public void __DelItem__(Slice slice) {
            if (slice.step == 0)
            {
                throw new ValueError("slice step cannot be zero");
            }

            if (slice.step < 0)
            {
                // Negative slice is no-op
                return;
            }

            (uint start, uint end) = _NormalizeSlice(slice.start, slice.end);

            if (slice.step == 1) {
                if (start == end) {
                    // No-op
                    return;
                }

                // Replace entire given range
                _DeleteSliceSingleStep(start, end);
            } else {
                _DeleteSliceMultiStep(start, end, (uint)slice.step);
            }
        }

        private void _DeleteSliceSingleStep(uint start, uint end) {
            _list.RemoveRange((int)start, (int)(end - start));
        }

        private void _DeleteSliceMultiStep(uint start, uint end, uint step) {
            var numElemsToChange = Slice.Len((int)start, (int)end, (int)step);

            uint elemsChanged = 0;

            for (uint i = start; i < end && elemsChanged < numElemsToChange; ++i) {
                if ((i - start) % step == 0) {
                    _list.RemoveAt((int)(i - elemsChanged));

                    ++elemsChanged;
                }
            }
        }

        public void __SetItem__(int index, T value)
        {
            index = (int)_NormalizeIndex(index, false, false);
            _list[index] = value;
        }

        public void __SetItem__(List<T> other) {
            _list.Clear();
            _list.EnsureCapacity(other._list.Count);
            _list.AddRange(other._list);
        }

        public void __SetItem__(Slice slice, List<T> other) {
            if (slice.step == 0)
            {
                throw new ValueError("slice step cannot be zero");
            }

            if (slice.step < 0)
            {
                // Negative slice is no-op
                return;
            }

            (uint start, uint end) = _NormalizeSlice(slice.start, slice.end);

            if (slice.step == 1) {
                if (start == end) {
                    // Simple insertion
                    _SetSliceInsertion(other, start);
                }  else {
                    // Replace entire given range
                    _SetSliceSingleStep(other, start, end);
                }
            } else {
                _SetSliceMultiStep(other, start, end, (uint)slice.step);
            }
        }

        private void _SetSliceInsertion(List<T> other, uint start) {
            if (other._list.Count == 0) {
                return;
            }

            _list.EnsureCapacity(_list.Count + other._list.Count);
            _list.InsertRange((int)start, other);
        }

        private void _SetSliceSingleStep(List<T> other, uint start, uint end) {
            var numOldElems = end - start;
            var numNewElems = other.__Len__();

            if (numOldElems != numNewElems) {
                _SetSliceSingleStepReplacement(other, start, numOldElems,
                                                numNewElems);
            } else {
                // Trivial case, replace 1-to-1
                for (uint i = start; i < end; ++i) {
                    _list[(int)i] = other[(int)(i - start)];
                }
            }
        }

        private void _SetSliceSingleStepReplacement(List<T> other,
                                            uint start,
                                            uint numOldElems,
                                            uint numNewElems) {
            if (numNewElems > numOldElems) {
                var numExtraElems = numNewElems - numOldElems;
                _list.EnsureCapacity(_list.Count + (int)numExtraElems);
            }

            // TODO: Can optimize for fewer element shifts
            _list.RemoveRange((int)start, (int)numOldElems);
            _list.InsertRange((int)start, other);
        }

        void _SetSliceMultiStep(List<T> other, uint start, uint end, uint step) {
            var numElemsToChange = Slice.Len((int)start, (int)end, (int)step);

            if (other._list.Count != numElemsToChange) {
                throw new ValueError($"Attempt to assign sequence of size "
                    + $"{other._list.Count} to extended slice of size "
                    + $"{numElemsToChange}");
            }

            uint elemsChanged = 0;

            for (uint i = start; i < end && elemsChanged < numElemsToChange; ++i) {
                if ((i - start) % step == 0) {
                    _list[(int)i] = other[(int)elemsChanged];

                    ++elemsChanged;
                }
            }
        }
    }
}
