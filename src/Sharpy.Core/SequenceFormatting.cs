using System;
using System.Collections;
using System.Collections.Generic;

namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Formats a value that is a PLAIN CLR sequence — enumerable, but carrying no rendering of
        /// its own — as the Python list its semantic type says it is. Returns false for everything
        /// else, leaving the caller's existing arms untouched.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Without this, a value whose semantic type is <c>list[str]</c> printed as
        /// <c>System.Linq.Enumerable+IteratorSelectIterator`2[System.Char,System.String]</c> — the
        /// name of whatever LINQ node happened to build it (#1453). The Sharpy type system already
        /// calls that value a list, and binding it to a <c>list[str]</c> annotation printed
        /// <c>['c', 'b', 'a']</c>; only the unbound display position leaked. Display positions are
        /// read-only, so materializing to render mutates nothing (the #1251 concern does not reach
        /// here).
        /// </para>
        /// <para>
        /// The test is "does this value render itself?", not a list of types. Anything that
        /// overrides <see cref="object.ToString"/> is left alone, which is what keeps the arm from
        /// swallowing the values that already have a Python rendering — the Sharpy collections
        /// (<c>List</c>/<c>Dict</c>/<c>Set</c>) and, importantly, <c>Iterator&lt;T&gt;</c>, whose
        /// <c>&lt;iterator object&gt;</c>/<c>&lt;reversed object&gt;</c> reprs are deliberate and
        /// must NOT become list output (they are lazy, and printing one would consume it). Written
        /// as a property of the value rather than a type list so a new sequence type is covered on
        /// arrival instead of being the next bug.
        /// </para>
        /// <para>
        /// Strings are excluded explicitly: <c>string</c> is <see cref="IEnumerable"/> and does
        /// override ToString, but the exclusion is stated rather than left to that coincidence.
        /// </para>
        /// </remarks>
        internal static bool TryFormatPlainClrSequence(object x, out string formatted)
        {
            formatted = string.Empty;

            if (x is string || x is not IEnumerable sequence)
            {
                return false;
            }

            if (RendersItself(x.GetType()))
            {
                return false;
            }

            var items = new List<string>();
            foreach (var item in sequence)
            {
                items.Add(Repr(item));
            }

            formatted = "[" + string.Join(", ", items) + "]";
            return true;
        }

        /// <summary>
        /// Whether the type provides its own <see cref="object.ToString"/> — i.e. printing it would
        /// produce something its author chose, rather than a CLR type name.
        /// </summary>
        private static bool RendersItself(System.Type type)
        {
            // System.Type spelled out: the enclosing class declares a `Type(object?)` builtin, which
            // shadows the bare name here.
            var toString = type.GetMethod("ToString", System.Type.EmptyTypes);
            return toString != null && toString.DeclaringType != typeof(object);
        }
    }
}
