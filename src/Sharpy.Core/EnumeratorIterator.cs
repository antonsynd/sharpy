using System.Collections.Generic;
using System;
namespace Sharpy
{
    /// <summary>
    /// Generic iterator wrapper that adapts an IEnumerator to the Iterator interface.
    /// </summary>
    internal sealed class EnumeratorIterator<T> : Iterator<T>
    {
        private readonly IEnumerator<T> _enumerator;
        private readonly string _repr;

        /// <param name="enumerator">The enumerator to adapt.</param>
        /// <param name="repr">
        /// How this iterator renders. CPython names an iterator after WHAT PRODUCED IT, not after
        /// the adapter type — `iter([1,2])` is a `list_iterator` while `reversed(x)` on the same
        /// list is a `reversed`/`list_reverseiterator`. Both land on this one adapter here, so the
        /// producer passes the name rather than the type carrying it.
        ///
        /// <para>
        /// DO NOT "IMPROVE" THIS BY THREADING THE SOURCE CONTAINER'S NAME THROUGH. It looks like the
        /// missing fidelity, and it is not: CPython has `list_iterator` because CPython has `list`,
        /// whereas `iter()` here accepts ANY CLR `IEnumerable`, and for most of those CPython never
        /// assigned a name at all. Threading would mean INVENTING plausible-looking names for
        /// producers CPython has never seen — fabrication that reads perfectly well, which is the
        /// hardest kind to notice. The default stays deliberately generic, and the address-free form
        /// below is the larger divergence anyway; both are recorded in `docs/deviations.yaml`
        /// (iterator-repr-no-address).
        /// </para>
        /// </param>
        public EnumeratorIterator(IEnumerator<T> enumerator, string repr = "<iterator object>")
        {
            _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
            _repr = repr;
        }

        /// <inheritdoc/>
        public override string ToString() => _repr;

        /// <inheritdoc/>
        public override bool MoveNext()
        {
            if (_enumerator.MoveNext())
            {
                _current = _enumerator.Current;
                return true;
            }

            _current = default;
            return false;
        }
    }
}
