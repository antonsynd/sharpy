using System;

namespace Sharpy
{
    /// <summary>
    /// Raised when a mapping (dictionary) key is not found.
    /// </summary>
    /// <remarks>
    /// Python's <c>KeyError</c> carries the missing key itself. Its <c>__str__</c> is the key's
    /// <c>repr</c> and its <c>__repr__</c> is <c>KeyError(&lt;repr of the key&gt;)</c>:
    /// <c>str(KeyError('k'))</c> is <c>'k'</c>, <c>repr(KeyError('k'))</c> is <c>KeyError('k')</c>,
    /// <c>str(KeyError(3))</c> is <c>3</c>, and <c>str(KeyError())</c> is empty. This class is the one
    /// place that rule lives: every raiser passes the RAW key (never a pre-quoted message), and
    /// <see cref="Message"/> and <see cref="IRepr.Repr"/> render it, so <c>str(e)</c>, <c>print(e)</c>,
    /// an f-string hole, <c>repr(e)</c> and the unhandled-exception line agree.
    /// </remarks>
    public class KeyError : Exception, IRepr
    {
        private readonly object? _key;
        private readonly bool _hasKey;

        /// <summary>Create a KeyError with no key: python's <c>KeyError()</c>, whose str is empty.</summary>
        public KeyError() : base() { }

        /// <summary>Create a KeyError for the missing <paramref name="key"/> (any value, as in python).</summary>
        public KeyError(object? key) : base()
        {
            _key = key;
            _hasKey = true;
        }

        /// <summary>Python's <c>str(e)</c>: the repr of the key, or empty for <c>KeyError()</c>.</summary>
        public override string Message => _hasKey ? Builtins.Repr(_key) : string.Empty;

        /// <summary>Python's <c>repr(e)</c>: <c>KeyError('k')</c>, <c>KeyError(3)</c>, <c>KeyError()</c>.</summary>
        string IRepr.Repr() =>
            PyFormat.PyDunderName(GetType()) + "(" + (_hasKey ? Builtins.Repr(_key) : string.Empty) + ")";
    }
}
