using System.Collections;

namespace Sharpy
{
    /// <summary>
    /// Non-generic Pythonic set protocol interface implemented by every
    /// <see cref="Set{T}"/> instantiation via explicit boxing adapters.
    /// <para>
    /// Used as the binding type when pattern-matching a bare <c>case set() as s:</c>
    /// against an <c>object</c> scrutinee. The type-erased surface lets the emitter's
    /// name-mangled member access resolve structurally (e.g. <c>s.Add(x)</c>,
    /// <c>s.Contains(x)</c>) without special-casing every set member in codegen.
    /// </para>
    /// <para>
    /// <b>Type-erasure semantics (Axiom 1 — .NET first):</b> reads with a wrong-typed
    /// item behave as if the item is absent (false / silently ignored); writes cast to the
    /// underlying generic parameter and let .NET throw on mismatch.
    /// </para>
    /// <para>
    /// Inherits non-generic <see cref="IEnumerable"/> (not <c>IEnumerable&lt;object?&gt;</c>)
    /// to avoid adding a second generic IEnumerable to Set&lt;T&gt; which would break
    /// LINQ type inference. Foreach over ISet yields elements as <c>object</c>.
    /// </para>
    /// </summary>
    public interface ISet : ISized, IEnumerable
    {
        /// <summary>
        /// Add an element to the set (no effect if already present).
        /// Casts the item to the underlying generic type.
        /// </summary>
        void Add(object? item);

        /// <summary>
        /// Remove an element from the set if present (no error if not present).
        /// Silently ignores wrong-typed items.
        /// </summary>
        void Discard(object? item);

        /// <summary>
        /// Remove an element from the set.
        /// Throws <see cref="KeyError"/> if the element is not present.
        /// </summary>
        void Remove(object item);

        /// <summary>
        /// Remove and return an arbitrary element from the set.
        /// Throws <see cref="KeyError"/> if the set is empty.
        /// </summary>
        object? Pop();

        /// <summary>Remove all elements from the set.</summary>
        void Clear();

        /// <summary>Return a shallow copy of the set.</summary>
        ISet Copy();

        /// <summary>Return whether the set contains the specified element.</summary>
        bool Contains(object item);

        /// <summary>
        /// Update the set, adding elements from the iterable.
        /// </summary>
        void Update(IEnumerable items);
    }
}
