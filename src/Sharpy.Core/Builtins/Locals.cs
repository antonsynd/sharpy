using System;

namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Records that a runtime-checked local has been assigned and forwards the stored value, so
        /// a store can set the local's assigned-flag inline — in statement position
        /// (<c>n = Builtins.Assigned(ref __n_assigned, 5);</c>) and in walrus/expression position
        /// alike. Used by the emitter for a local whose assignment a <c>with</c> block can suppress
        /// (#1839): every store to the local is wrapped so a later read can tell "assigned" from
        /// "unset" at runtime.
        /// </summary>
        /// <typeparam name="T">The stored value's type.</typeparam>
        /// <param name="flag">The local's assigned-flag; set to <c>true</c>.</param>
        /// <param name="value">The value being stored.</param>
        /// <returns><paramref name="value"/> unchanged.</returns>
        public static T Assigned<T>(ref bool flag, T value)
        {
            flag = true;
            return value;
        }

        /// <summary>
        /// Reads a runtime-checked local: returns <paramref name="value"/> when
        /// <paramref name="flag"/> is set, else raises <see cref="UnboundLocalError"/> naming the
        /// variable — Python's <c>UnboundLocalError</c> semantics for a local read before assignment
        /// (#1839).
        /// </summary>
        /// <typeparam name="T">The local's type.</typeparam>
        /// <param name="flag">The local's assigned-flag.</param>
        /// <param name="value">The local's current value (a <c>default!</c> placeholder when unset).</param>
        /// <param name="name">The Python name of the local, for the error message.</param>
        /// <returns><paramref name="value"/> when <paramref name="flag"/> is set.</returns>
        /// <exception cref="UnboundLocalError">When <paramref name="flag"/> is <c>false</c>.</exception>
        public static T CheckedLocal<T>(bool flag, T value, string name)
        {
            if (!flag)
            {
                throw UnboundLocalError.ForName(name);
            }
            return value;
        }
    }
}
