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

        public EnumeratorIterator(IEnumerator<T> enumerator)
        {
            _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
        }

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
