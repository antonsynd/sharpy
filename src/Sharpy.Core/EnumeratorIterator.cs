using System.Collections.Generic;
using System;
namespace Sharpy
{
    using System.Collections;

    /// <summary>
    /// Generic iterator wrapper that adapts an IEnumerator to the Iterator interface.
    /// </summary>
    internal sealed class EnumeratorIterator<T> : Iterator<T>
    {
        private readonly IEnumerator<T> _enumerator;
        private bool _started;

        public EnumeratorIterator(IEnumerator<T> enumerator)
        {
            _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
            _started = false;
        }

        /// <summary>
        /// Deprecated: Use <see cref="Iterator{T}.Next()"/> instead.
        /// </summary>
        public override T __Next__()
        {
            if (!_started)
            {
                _started = true;
                if (!_enumerator.MoveNext())
                {
                    throw new StopIteration();
                }
                return _enumerator.Current;
            }

            if (_enumerator.MoveNext())
            {
                return _enumerator.Current;
            }

            throw new StopIteration();
        }
    }
}
