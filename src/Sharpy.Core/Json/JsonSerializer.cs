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
        public static string Serialize(object? obj, int indent = -1, bool sortKeys = false, bool ensureAscii = true)
        {
            var sb = new StringBuilder();
            SerializeValue(sb, obj, indent, sortKeys, ensureAscii, 0);
            return sb.ToString();
        }

        private static void SerializeValue(
            StringBuilder sb,
            object? value,
            int indent,
            bool sortKeys,
            bool ensureAscii,
            int currentIndent)
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
                SerializeDouble(sb, d);
                return;
            }

            if (value is float f)
            {
                SerializeDouble(sb, f);
                return;
            }

            // Handle Dict<string, object?> and Dict<string, object>
            if (value is IDictionary<string, object?> dictNullable)
            {
                SerializeDict(sb, dictNullable, indent, sortKeys, ensureAscii, currentIndent);
                return;
            }

            if (value is IDictionary<string, object> dictNonNull)
            {
                SerializeDictNonNull(sb, dictNonNull, indent, sortKeys, ensureAscii, currentIndent);
                return;
            }

            // Handle Dict<string, V> for value-type V via the IStrKeyDictionary
            // interface (compile-time dispatch; no reflection).
            if (value is IStrKeyDictionary strKeyDict)
            {
                SerializeStrKeyDict(sb, strKeyDict, indent, sortKeys, ensureAscii, currentIndent);
                return;
            }

            // Handle List<object?> and other IEnumerable<object?>
            if (value is IEnumerable<object?> enumerable && !(value is string))
            {
                SerializeEnumerable(sb, enumerable, indent, sortKeys, ensureAscii, currentIndent);
                return;
            }

            // Handle generic collections with value-type elements (e.g. List<int>, Set<int>)
            // which don't implement IEnumerable<object?> due to C# covariance limitations.
            // Must come after IDictionary checks to avoid serializing dicts as arrays.
            if (value is IEnumerable nonGenericEnumerable && !(value is string))
            {
                SerializeNonGenericEnumerable(sb, nonGenericEnumerable, indent, sortKeys, ensureAscii, currentIndent);
                return;
            }

            // Fallback: try to convert to string
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

        private static void SerializeDouble(StringBuilder sb, double d)
        {
            if (double.IsInfinity(d) || double.IsNaN(d))
            {
                throw new ValueError(
                    "Out of range float values are not JSON compliant");
            }

            string repr = d.ToString("R", CultureInfo.InvariantCulture);

            // Ensure it looks like a float (has . or e)
            if (repr.IndexOf('.') < 0 && repr.IndexOf('E') < 0 && repr.IndexOf('e') < 0)
            {
                repr += ".0";
            }

            sb.Append(repr);
        }

        private static void SerializeDict(
            StringBuilder sb,
            IDictionary<string, object?> dict,
            int indent,
            bool sortKeys,
            bool ensureAscii,
            int currentIndent)
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
                    sb.Append(',');
                    if (!pretty)
                    {
                        sb.Append(' ');
                    }
                }

                first = false;

                if (pretty)
                {
                    sb.Append('\n');
                    sb.Append(' ', nextIndent);
                }

                SerializeString(sb, key, ensureAscii);
                sb.Append(':');
                sb.Append(' ');

                SerializeValue(sb, dict[key], indent, sortKeys, ensureAscii, nextIndent);
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
            int currentIndent)
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
                    sb.Append(',');
                    if (!pretty)
                    {
                        sb.Append(' ');
                    }
                }

                first = false;

                if (pretty)
                {
                    sb.Append('\n');
                    sb.Append(' ', nextIndent);
                }

                SerializeString(sb, key, ensureAscii);
                sb.Append(':');
                sb.Append(' ');

                SerializeValue(sb, dict[key], indent, sortKeys, ensureAscii, nextIndent);
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
            int currentIndent)
        {
            bool pretty = indent >= 0;
            int nextIndent = currentIndent + (pretty ? indent : 0);

            sb.Append('[');

            bool first = true;
            foreach (object? item in enumerable)
            {
                if (!first)
                {
                    sb.Append(',');
                    if (!pretty)
                    {
                        sb.Append(' ');
                    }
                }

                first = false;

                if (pretty)
                {
                    sb.Append('\n');
                    sb.Append(' ', nextIndent);
                }

                SerializeValue(sb, item, indent, sortKeys, ensureAscii, nextIndent);
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
            int currentIndent)
        {
            bool pretty = indent >= 0;
            int nextIndent = currentIndent + (pretty ? indent : 0);

            sb.Append('[');

            bool first = true;
            foreach (object? item in enumerable)
            {
                if (!first)
                {
                    sb.Append(',');
                    if (!pretty)
                    {
                        sb.Append(' ');
                    }
                }

                first = false;

                if (pretty)
                {
                    sb.Append('\n');
                    sb.Append(' ', nextIndent);
                }

                SerializeValue(sb, item, indent, sortKeys, ensureAscii, nextIndent);
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
            int currentIndent)
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
                    sb.Append(',');
                    if (!pretty)
                    {
                        sb.Append(' ');
                    }
                }

                first = false;

                if (pretty)
                {
                    sb.Append('\n');
                    sb.Append(' ', nextIndent);
                }

                SerializeString(sb, entry.Key, ensureAscii);
                sb.Append(':');
                sb.Append(' ');

                SerializeValue(sb, entry.Value, indent, sortKeys, ensureAscii, nextIndent);
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
