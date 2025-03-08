using Sharpy.Collections.Interfaces;

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
            _list.Insert((int)_NormalizeIndex(i, true), x);
        }

        /// <summary>
        /// Remove the item at the given position in the list, and return it.
        /// If no index is specified, a.Pop() removes and returns the last
        /// item in the list. It raises an IndexError if the list is empty or
        /// the index is outside the list range.
        /// </summary>
        public T Pop(int i = -1)
        {
            if (__Len__() == 0)
            {
                throw new IndexError("pop from empty list");
            }

            try
            {
                i = (int)_NormalizeIndex(i);
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

        public void __DelItem__(int index, T x)
        {

        }

        public void __DelItem__(Slice slice) {

        }

        public void __SetItem__(int index, T value)
        {
            index = (int)_NormalizeIndex(index);
            _list[index] = value;
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

            (int start, int end) = ((int, int))_NormalizeSlice(slice.start, slice.end);
        }

        // void _SetSliceSingleStep(List<T> other,
        //                             SliceParams<iterator> slice_params) {
        //     const auto start_it = std::move(slice_params.start_it);
        //     const auto start = slice_params.start;
        //     const auto end = slice_params.end;

        //     const auto num_old_elems = end - start;
        //     const auto num_new_elems = other.data_.v_.size();

        //     if (num_old_elems < num_new_elems) {
        //         _SetSliceSingleStepExpanding(other, start, num_old_elems,
        //                                         num_new_elems);
        //     } else if (num_old_elems > num_new_elems) {
        //         _SetSliceSingleStepReducing(other, start_it, num_old_elems,
        //                                     num_new_elems);
        //     } else {
        //     // Trivial case, replace 1-to-1
        //     std::copy(other.data_.v_.begin(), other.data_.v_.end(), start_it);
        //     }
        // }

        // void _SetSliceSingleStepExpanding(const self& other,
        //                                     size_t start,
        //                                     size_t num_old_elems,
        //                                     size_t num_new_elems) {
        //     const auto num_extra_elems = num_new_elems - num_old_elems;
        //     data_.v_.resize(data_.v_.size() + num_extra_elems);

        //     // Recalculate the start iterator in case the resize() call
        //     // invalidated it
        //     const auto start_it = data_.v_.begin() + start;

        //     // Shift elements from the starting position to make room for the
        //     // incoming ones
        //     std::shift_right(start_it, data_.v_.end(), num_extra_elems);

        //     // Copy into the desired range
        //     std::copy(other.data_.v_.begin(), other.data_.v_.end(), start_it);
        // }

        // void _SetSliceSingleStepReducing(const self& other,
        //                                     iterator start_it,
        //                                     size_t num_old_elems,
        //                                     size_t num_new_elems) {
        //     const auto num_elems_to_remove = num_old_elems - num_new_elems;

        //     // Copy into desired range
        //     start_it =
        //         std::copy(other.data_.v_.begin(), other.data_.v_.end(), start_it);
        //     const auto end_it = start_it + num_elems_to_remove;

        //     // Erase leftover elements
        //     data_.v_.erase(start_it, end_it);
        // }

        // void _SetSliceMultiStep(const self& other,
        //                             SliceParams<iterator> slice_params) {
        //     const auto [start, end, step, start_it, end_it] = std::move(slice_params);
        //     const auto num_old_elems = GetNumberOfElementsInSlice(start, end, step);

        //     if (other.data_.v_.size() != num_old_elems) {
        //     throw ValueError(
        //         "ValueError: attempt to assign sequence of size {} to extended "
        //         "slice "
        //         "of size {}");
        //     }

        //     size_t idx = 0;
        //     auto other_it = other.data_.v_.begin();
        //     const auto other_end = other.data_.v_.end();

        //     std::for_each(start_it, end_it,
        //                 [&idx, step, &other_it, &other_end](value_type& elem) {
        //                     if (other_it == other_end) {
        //                     // Shouldn't happen, but here as a fail-safe
        //                     return;
        //                     }

        //                     if (idx % step == 0) {
        //                     elem = *other_it;
        //                     ++other_it;
        //                     }

        //                     ++idx;
        //                 });
        // }
    }
}
