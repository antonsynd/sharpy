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
        /// The number of elements. Mirrors the (explicitly-implemented) ICollection.Count and is
        /// exposed publicly so that C# list patterns over Sharpy lists are "countable"
        /// (e.g. <c>case [a, b, *rest]</c>). Sharpy code should prefer <c>len(x)</c>.
        /// </summary>
        public int Length => _list.Count;

        /// <summary>
        /// Element access by <see cref="System.Index"/>, enabling C# list-pattern element matching.
        /// Uses from-start/from-end offsets (no Python negative-index wraparound).
        /// </summary>
        public T this[System.Index index] => _list[index.GetOffset(_list.Count)];

        /// <summary>
        /// Sub-range access by <see cref="System.Range"/>, enabling the slice (<c>..</c>) element of
        /// a C# list pattern (e.g. the <c>*rest</c> capture). Returns a new list.
        /// </summary>
        public List<T> this[System.Range range]
        {
            get
            {
                var (offset, length) = range.GetOffsetAndLength(_list.Count);
                return GetSlice(new Slice(offset, offset + length, 1));
            }
        }

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
            get => GetSlice(new Slice(start, end, 1));
            set => SetSlice(new Slice(start, end, 1), value);
        }

        /// <summary>
        /// Gets or sets a slice of the list using start, end, and step.
        /// </summary>
        public List<T> this[int start, int end, int step]
        {
            get => GetSlice(new Slice(start, end, step));
            set => SetSlice(new Slice(start, end, step), value);
        }

        #endregion

        #region Get Item Methods

        /// <summary>
        /// Returns a slice of the list.
        /// </summary>
        /// <exception cref="ValueError">Thrown if slice step is zero.</exception>
        public List<T> GetSlice(Slice slice)
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
        /// Sets a slice of the list from an enumerable.
        /// </summary>
        public void SetSlice(Slice slice, IEnumerable<T> other)
        {
            if (other is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            SetSlice(slice, new List<T>(other));
        }

        /// <summary>
        /// Sets a slice of the list from another list.
        /// </summary>
        /// <exception cref="TypeError">Thrown if <paramref name="other"/> is null.</exception>
        /// <exception cref="ValueError">Thrown if slice step is zero or assignment size mismatches extended slice.</exception>
        public void SetSlice(Slice slice, List<T> other)
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
                throw new NotImplementedError("negative-step slice assignment is not yet supported");
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
                    _list[i] = other._list[i - start];
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
        public void DeleteAt(int index)
        {
            _list.RemoveAt(Sharpy.Index.Normalize(index, _list.Count, false, false));
        }

        /// <summary>
        /// Deletes a slice of the list.
        /// </summary>
        /// <exception cref="ValueError">Thrown if slice step is zero.</exception>
        public void DeleteSlice(Slice slice)
        {
            if (slice.step == 0)
            {
                throw new ValueError("slice step cannot be zero");
            }

            if (slice.step < 0)
            {
                throw new NotImplementedError("negative-step slice deletion is not yet supported");
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
