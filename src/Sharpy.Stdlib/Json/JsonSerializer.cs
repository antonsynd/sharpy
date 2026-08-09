using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Minimal JSON serializer.
    /// Supports Dict, List, string, int, long, double, bool, and null.
    /// </summary>
    internal static class JsonSerializer
    {
        /// <summary>
        /// Serialize a Sharpy object to a JSON formatted string.
        /// </summary>
        public static string Serialize(
            object? obj,
            int indent = -1,
            bool sortKeys = false,
            bool ensureAscii = true,
            string? itemSeparator = null,
            string? keySeparator = null,
            Func<object, object?>? defaultFunc = null,
            bool allowNan = true)
        {
            var sb = new StringBuilder();
            SerializeValue(sb, obj, indent, sortKeys, ensureAscii, 0, itemSeparator, keySeparator, defaultFunc, allowNan);
            return sb.ToString();
        }

        private static void SerializeValue(
            StringBuilder sb,
            object? value,
            int indent,
            bool sortKeys,
            bool ensureAscii,
            int currentIndent,
            string? itemSeparator,
            string? keySeparator,
            Func<object, object?>? defaultFunc,
            bool allowNan)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }

            if (value is bool b)
            {
                sb.Append(b ? "true" : "false");
                return;
            }

            if (value is string s)
            {
                SerializeString(sb, s, ensureAscii);
                return;
            }

            if (value is int i)
            {
                sb.Append(i.ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (value is long l)
            {
                sb.Append(l.ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (value is double d)
            {
                SerializeDouble(sb, d, allowNan);
                return;
            }

            if (value is float f)
            {
                SerializeSingle(sb, f, allowNan);
                return;
            }

            // Handle Dict<string, object?> and Dict<string, object>
            if (value is IDictionary<string, object?> dictNullable)
            {
                SerializeDict(sb, dictNullable, indent, sortKeys, ensureAscii, currentIndent, itemSeparator, keySeparator, defaultFunc, allowNan);
                return;
            }

            if (value is IDictionary<string, object> dictNonNull)
            {
                SerializeDictNonNull(sb, dictNonNull, indent, sortKeys, ensureAscii, currentIndent, itemSeparator, keySeparator, defaultFunc, allowNan);
                return;
            }

            // Handle Dict<string, V> for value-type V via the IStrKeyDictionary
            // interface (compile-time dispatch; no reflection).
            if (value is IStrKeyDictionary strKeyDict)
            {
                SerializeStrKeyDict(sb, strKeyDict, indent, sortKeys, ensureAscii, currentIndent, itemSeparator, keySeparator, defaultFunc, allowNan);
                return;
            }

            // Handle List<object?> and other IEnumerable<object?>
            if (value is IEnumerable<object?> enumerable && !(value is string))
            {
                SerializeEnumerable(sb, enumerable, indent, sortKeys, ensureAscii, currentIndent, itemSeparator, keySeparator, defaultFunc, allowNan);
                return;
            }

            // Handle generic collections with value-type elements (e.g. List<int>, Set<int>)
            // which don't implement IEnumerable<object?> due to C# covariance limitations.
            // Must come after IDictionary checks to avoid serializing dicts as arrays.
            if (value is IEnumerable nonGenericEnumerable && !(value is string))
            {
                SerializeNonGenericEnumerable(sb, nonGenericEnumerable, indent, sortKeys, ensureAscii, currentIndent, itemSeparator, keySeparator, defaultFunc, allowNan);
                return;
            }

            // If a default callback was provided, give it a chance to convert the value
            // into a JSON-serializable representation before failing.
            if (defaultFunc != null)
            {
                object? replacement = defaultFunc(value);

                // Guard against infinite recursion: if the callback returns the same
                // object, raise the same TypeError Python raises in this situation.
                if (ReferenceEquals(replacement, value))
                {
                    throw new TypeError(
                        "Object of type " + value.GetType().Name + " is not JSON serializable");
                }

                // Pass null as defaultFunc to prevent unbounded recursion on
                // values the callback returns that are themselves non-serializable.
                SerializeValue(sb, replacement, indent, sortKeys, ensureAscii, currentIndent, itemSeparator, keySeparator, null, allowNan);
                return;
            }

            // Fallback: not serializable and no callback provided.
            throw new TypeError(
                "Object of type " + value.GetType().Name + " is not JSON serializable");
        }

        private static void SerializeString(StringBuilder sb, string s, bool ensureAscii)
        {
            sb.Append('"');

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];

                switch (c)
                {
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (c < ' ')
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else if (ensureAscii && c > 127)
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }

                        break;
                }
            }

            sb.Append('"');
        }

        /// <summary>
        /// Serializes a finite double using the one float-formatting authority (#1229).
        /// </summary>
        /// <remarks>
        /// <c>json.dumps</c> spells a float exactly as <c>repr</c> does in CPython, and
        /// <see cref="Builtins.FormatFloat(double)"/> IS that rule — so the authority applies here
        /// verbatim, with no wire-format adjustment.
        ///
        /// <para>
        /// This replaces <c>ToString("R")</c> plus a hand-rolled <c>.0</c> append, which diverged on
        /// two axes: .NET spells the exponent <c>E+20</c> where CPython spells it <c>e+20</c>, and
        /// .NET stays positional at <c>1e16</c> (<c>10000000000000000</c>) where CPython has already
        /// switched to exponent form. The append is deleted rather than kept — producing <c>1.0</c>
        /// for a whole value is part of the authority's contract, so a second append would be a
        /// redundant rule to keep in sync.
        /// </para>
        ///
        /// <para>
        /// Infinity and NaN emit CPython's <c>Infinity</c>/<c>-Infinity</c>/<c>NaN</c> tokens, its
        /// default; <c>allowNan: false</c> restores the throwing branch (#1296).
        /// </para>
        /// </remarks>
        private static void SerializeDouble(StringBuilder sb, double d, bool allowNan)
        {
            if (double.IsInfinity(d) || double.IsNaN(d))
            {
                sb.Append(NonFiniteToken(d, allowNan));
                return;
            }

            sb.Append(Builtins.FormatFloat(d));
        }

        /// <summary>
        /// Serializes a finite single-precision float at its OWN precision (#1229).
        /// </summary>
        /// <remarks>
        /// Widening to double before formatting changes the shortest-round-trip digits — the same
        /// defect #1204 fixed in pprint — so the <c>float</c> overload of the authority is used rather
        /// than letting the value widen at the call. A <c>float32</c> therefore serializes as the text
        /// Sharpy's own <c>str()</c> would produce for it.
        /// </remarks>
        private static void SerializeSingle(StringBuilder sb, float f, bool allowNan)
        {
            if (float.IsInfinity(f) || float.IsNaN(f))
            {
                sb.Append(NonFiniteToken(f, allowNan));
                return;
            }

            sb.Append(Builtins.FormatFloat(f));
        }

        /// <summary>
        /// CPython's spelling for a non-finite float, or its <c>allow_nan=False</c> error (#1296).
        /// </summary>
        /// <remarks>
        /// <c>Infinity</c>/<c>-Infinity</c>/<c>NaN</c> are CPython's own extension to JSON — strict
        /// JSON has no such tokens — and they are its DEFAULT. Sharpy threw unconditionally while
        /// borrowing CPython's <c>allow_nan=False</c> message verbatim, which is what showed the
        /// inversion was accidental rather than a decision: the module was emitting the strict
        /// path's diagnostic from the default path.
        ///
        /// <para>The message now carries the value, as CPython's does
        /// (<c>Out of range float values are not JSON compliant: inf</c>) — a caller that opted
        /// into strictness is told which value tripped it.</para>
        /// </remarks>
        private static string NonFiniteToken(double value, bool allowNan)
        {
            if (!allowNan)
            {
                throw new ValueError(
                    "Out of range float values are not JSON compliant: "
                    + Builtins.Repr(value));
            }

            if (double.IsNaN(value))
            {
                return "NaN";
            }

            return double.IsPositiveInfinity(value) ? "Infinity" : "-Infinity";
        }

        private static void SerializeDict(
            StringBuilder sb,
            IDictionary<string, object?> dict,
            int indent,
            bool sortKeys,
            bool ensureAscii,
            int currentIndent,
            string? itemSeparator,
            string? keySeparator,
            Func<object, object?>? defaultFunc,
            bool allowNan)
        {
            var keys = new System.Collections.Generic.List<string>(dict.Keys);

            if (sortKeys)
            {
                keys.Sort(StringComparer.Ordinal);
            }

            if (keys.Count == 0)
            {
                sb.Append("{}");
                return;
            }

            bool pretty = indent >= 0;
            int nextIndent = currentIndent + (pretty ? indent : 0);

            sb.Append('{');

            bool first = true;
            foreach (string key in keys)
            {
                if (!first)
                {
                    if (pretty)
                    {
                        sb.Append(',');
                    }
                    else
                    {
                        sb.Append(itemSeparator ?? ", ");
                    }
                }

                first = false;

                if (pretty)
                {
                    sb.Append('\n');
                    sb.Append(' ', nextIndent);
                }

                SerializeString(sb, key, ensureAscii);
                sb.Append(keySeparator ?? ": ");

                SerializeValue(sb, dict[key], indent, sortKeys, ensureAscii, nextIndent, itemSeparator, keySeparator, defaultFunc, allowNan);
            }

            if (pretty)
            {
                sb.Append('\n');
                sb.Append(' ', currentIndent);
            }

            sb.Append('}');
        }

        private static void SerializeDictNonNull(
            StringBuilder sb,
            IDictionary<string, object> dict,
            int indent,
            bool sortKeys,
            bool ensureAscii,
            int currentIndent,
            string? itemSeparator,
            string? keySeparator,
            Func<object, object?>? defaultFunc,
            bool allowNan)
        {
            var keys = new System.Collections.Generic.List<string>(dict.Keys);

            if (sortKeys)
            {
                keys.Sort(StringComparer.Ordinal);
            }

            if (keys.Count == 0)
            {
                sb.Append("{}");
                return;
            }

            bool pretty = indent >= 0;
            int nextIndent = currentIndent + (pretty ? indent : 0);

            sb.Append('{');

            bool first = true;
            foreach (string key in keys)
            {
                if (!first)
                {
                    if (pretty)
                    {
                        sb.Append(',');
                    }
                    else
                    {
                        sb.Append(itemSeparator ?? ", ");
                    }
                }

                first = false;

                if (pretty)
                {
                    sb.Append('\n');
                    sb.Append(' ', nextIndent);
                }

                SerializeString(sb, key, ensureAscii);
                sb.Append(keySeparator ?? ": ");

                SerializeValue(sb, dict[key], indent, sortKeys, ensureAscii, nextIndent, itemSeparator, keySeparator, defaultFunc, allowNan);
            }

            if (pretty)
            {
                sb.Append('\n');
                sb.Append(' ', currentIndent);
            }

            sb.Append('}');
        }

        private static void SerializeNonGenericEnumerable(
            StringBuilder sb,
            IEnumerable enumerable,
            int indent,
            bool sortKeys,
            bool ensureAscii,
            int currentIndent,
            string? itemSeparator,
            string? keySeparator,
            Func<object, object?>? defaultFunc,
            bool allowNan)
        {
            bool pretty = indent >= 0;
            int nextIndent = currentIndent + (pretty ? indent : 0);

            sb.Append('[');

            bool first = true;
            foreach (object? item in enumerable)
            {
                if (!first)
                {
                    if (pretty)
                    {
                        sb.Append(',');
                    }
                    else
                    {
                        sb.Append(itemSeparator ?? ", ");
                    }
                }

                first = false;

                if (pretty)
                {
                    sb.Append('\n');
                    sb.Append(' ', nextIndent);
                }

                SerializeValue(sb, item, indent, sortKeys, ensureAscii, nextIndent, itemSeparator, keySeparator, defaultFunc, allowNan);
            }

            if (first)
            {
                // empty
                sb.Append(']');
                return;
            }

            if (pretty)
            {
                sb.Append('\n');
                sb.Append(' ', currentIndent);
            }

            sb.Append(']');
        }

        private static void SerializeEnumerable(
            StringBuilder sb,
            IEnumerable<object?> enumerable,
            int indent,
            bool sortKeys,
            bool ensureAscii,
            int currentIndent,
            string? itemSeparator,
            string? keySeparator,
            Func<object, object?>? defaultFunc,
            bool allowNan)
        {
            bool pretty = indent >= 0;
            int nextIndent = currentIndent + (pretty ? indent : 0);

            sb.Append('[');

            bool first = true;
            foreach (object? item in enumerable)
            {
                if (!first)
                {
                    if (pretty)
                    {
                        sb.Append(',');
                    }
                    else
                    {
                        sb.Append(itemSeparator ?? ", ");
                    }
                }

                first = false;

                if (pretty)
                {
                    sb.Append('\n');
                    sb.Append(' ', nextIndent);
                }

                SerializeValue(sb, item, indent, sortKeys, ensureAscii, nextIndent, itemSeparator, keySeparator, defaultFunc, allowNan);
            }

            if (first)
            {
                // empty
                sb.Append(']');
                return;
            }

            if (pretty)
            {
                sb.Append('\n');
                sb.Append(' ', currentIndent);
            }

            sb.Append(']');
        }

        private static void SerializeStrKeyDict(
            StringBuilder sb,
            IStrKeyDictionary strKeyDict,
            int indent,
            bool sortKeys,
            bool ensureAscii,
            int currentIndent,
            string? itemSeparator,
            string? keySeparator,
            Func<object, object?>? defaultFunc,
            bool allowNan)
        {
            var entries = new System.Collections.Generic.List<KeyValuePair<string, object?>>(
                strKeyDict.GetStringKeyEntries());

            if (sortKeys)
            {
                entries.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));
            }

            if (entries.Count == 0)
            {
                sb.Append("{}");
                return;
            }

            bool pretty = indent >= 0;
            int nextIndent = currentIndent + (pretty ? indent : 0);

            sb.Append('{');

            bool first = true;
            foreach (var entry in entries)
            {
                if (!first)
                {
                    if (pretty)
                    {
                        sb.Append(',');
                    }
                    else
                    {
                        sb.Append(itemSeparator ?? ", ");
                    }
                }

                first = false;

                if (pretty)
                {
                    sb.Append('\n');
                    sb.Append(' ', nextIndent);
                }

                SerializeString(sb, entry.Key, ensureAscii);
                sb.Append(keySeparator ?? ": ");

                SerializeValue(sb, entry.Value, indent, sortKeys, ensureAscii, nextIndent, itemSeparator, keySeparator, defaultFunc, allowNan);
            }

            if (pretty)
            {
                sb.Append('\n');
                sb.Append(' ', currentIndent);
            }

            sb.Append('}');
        }
    }
}
