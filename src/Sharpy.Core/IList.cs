using System.Collections;

namespace Sharpy
{
    /// <summary>
    /// Non-generic Pythonic list protocol interface implemented by every
    /// <see cref="List{T}"/> instantiation via explicit boxing adapters.
    /// <para>
    /// Used as the binding type when pattern-matching a bare <c>case list() as l:</c>
    /// against an <c>object</c> scrutinee. The type-erased surface lets the emitter's
    /// name-mangled member access resolve structurally (e.g. <c>l.Append(x)</c>,
    /// <c>l[i]</c>) without special-casing every list member in codegen.
    /// </para>
    /// <para>
    /// <b>Type-erasure semantics (Axiom 1 — .NET first):</b> reads box the underlying
    /// generic element to <c>object?</c>; writes cast from <c>object</c> to the
    /// underlying generic parameter and let .NET throw on mismatch.
    /// </para>
    /// <para>
    /// Inherits non-generic <see cref="IEnumerable"/> (not <c>IEnumerable&lt;object?&gt;</c>)
    /// to avoid adding a second generic IEnumerable to List&lt;T&gt; which would break
    /// LINQ type inference. Foreach over IList yields elements as <c>object</c>.
    /// </para>
    /// </summary>
    public interface IList : ISized, IEnumerable
    {
        /// <summary>
        /// Gets or sets the element at the specified index.
        /// Supports Python-style negative indexing.
        /// Set casts the value to the underlying generic type.
        /// </summary>
        object? this[object index] { get; set; }

        /// <summary>Add an item to the end of the list.</summary>
        void Append(object? item);

        /// <summary>Extend the list by appending all items from the iterable.</summary>
        void Extend(IEnumerable items);

        /// <summary>
        /// Insert an item at a given position.
        /// Supports Python-style negative indexing.
        /// </summary>
        void Insert(object index, object? item);

        /// <summary>
        /// Remove the item at the given position and return it.
        /// Default is -1 (the last item).
        /// Throws <see cref="IndexError"/> if the list is empty or index is out of range.
        /// </summary>
        object? Pop(int index = -1);

        /// <summary>
        /// Remove the first occurrence of the specified value.
        /// Throws <see cref="ValueError"/> if the value is not found.
        /// </summary>
        void Remove(object item);

        /// <summary>Remove all items from the list.</summary>
        void Clear();

        /// <summary>Return a shallow copy of the list.</summary>
        IList Copy();

        /// <summary>Reverse the elements of the list in place.</summary>
        void Reverse();

        /// <summary>Sort the items of the list in place.</summary>
        void Sort();

        /// <summary>
        /// Return the zero-based index of the first occurrence of the specified value.
        /// Throws <see cref="ValueError"/> if the value is not found.
        /// </summary>
        int Index(object item);

        /// <summary>Return the number of occurrences of the specified value.</summary>
        int CountOf(object item);

        /// <summary>Return whether the list contains the specified value.</summary>
        bool Contains(object item);
    }
}
