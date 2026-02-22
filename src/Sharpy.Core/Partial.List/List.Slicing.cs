using System.Collections.Generic;
using System.Linq;

namespace Sharpy
{
    /// <summary>
    /// Indexing and slicing operations for List&lt;T&gt;.
    /// </summary>
    public sealed partial class List<T>
    {
        #region Indexers

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
                index = Sharpy.Index.Normalize(index, _list.Count, false, false);
                return _list[index];
            }
            set
            {
                index = Sharpy.Index.Normalize(index, _list.Count, false, false);
                _list[index] = value;
            }
        }

        /// <summary>
        /// Gets or sets a slice of the list using start and end indices.
        /// </summary>
        public List<T> this[int start, int end]
        {
            get => __GetItem__(new Slice(start, end, 1));
            set => __SetItem__(new Slice(start, end, 1), value);
        }

        /// <summary>
        /// Gets or sets a slice of the list using start, end, and step.
        /// </summary>
        public List<T> this[int start, int end, int step]
        {
            get => __GetItem__(new Slice(start, end, step));
            set => __SetItem__(new Slice(start, end, step), value);
        }

        #endregion

        #region Get Item Methods

        /// <summary>
        /// Returns the element at the specified index.
        /// </summary>
        /// <remarks>
        /// Deprecated: Use the indexer <c>list[index]</c> instead.
        /// </remarks>
        public T __GetItem__(int index) => this[index];

        /// <summary>
        /// Returns a slice of the list.
        /// </summary>
        /// <remarks>
        /// Deprecated: Use the indexer <c>list[start, end]</c> or <c>list[start, end, step]</c> instead.
        /// </remarks>
        public List<T> __GetItem__(Slice slice)
        {
            if (slice.step == 0)
            {
                throw new ValueError("slice step cannot be zero");
            }

            return Slice.GetSlice(this, slice.start, slice.end, slice.step);
        }

        #endregion

        #region Set Item Methods

        /// <summary>
        /// Sets the element at the specified index.
        /// </summary>
        /// <remarks>
        /// Deprecated: Use the indexer <c>list[index] = value</c> instead.
        /// </remarks>
        public void __SetItem__(int index, T value) => this[index] = value;

        /// <summary>
        /// Sets a slice of the list from an enumerable.
        /// </summary>
        /// <remarks>
        /// Deprecated: Use <c>list[start, end] = other</c> instead.
        /// </remarks>
        public void __SetItem__(Slice slice, IEnumerable<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            __SetItem__(slice, new List<T>(other));
        }

        /// <summary>
        /// Sets a slice of the list from another list.
        /// </summary>
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

            _list.InsertRange(start, other);
        }

        private void _SetSliceSingleStep(List<T> other, int start, int end)
        {
            var numOldElems = end - start;
            var numNewElems = other._list.Count;

            if (numOldElems != numNewElems)
            {
                _SetSliceSingleStepReplacement(other, start, numOldElems, numNewElems);
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

        private void _SetSliceSingleStepReplacement(List<T> other, int start, int numOldElems, int numNewElems)
        {
            // Overwrite the overlapping portion in-place
            int overlapCount = System.Math.Min(numOldElems, numNewElems);
            for (int i = 0; i < overlapCount; i++)
            {
                _list[start + i] = other[i];
            }

            if (numNewElems > numOldElems)
            {
                // More new elements than old: insert the remainder
                _list.InsertRange(start + overlapCount, other._list.GetRange(overlapCount, numNewElems - overlapCount));
            }
            else if (numOldElems > numNewElems)
            {
                // Fewer new elements than old: remove the excess
                _list.RemoveRange(start + overlapCount, numOldElems - numNewElems);
            }
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

        #endregion

        #region Delete Item Methods

        /// <summary>
        /// Deletes the element at the specified index.
        /// </summary>
        /// <remarks>
        /// Deprecated: Use <c>list.Pop(index)</c> instead.
        /// </remarks>
        public void __DelItem__(int index)
        {
            _list.RemoveAt(Sharpy.Index.Normalize(index, _list.Count, false, false));
        }

        /// <summary>
        /// Deletes a slice of the list.
        /// </summary>
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

            (int start, int end) = Slice.Normalize(slice.start, slice.end, _list.Count);

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

        #endregion
    }
}
