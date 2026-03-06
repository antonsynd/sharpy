using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// JSON serializer. No external dependencies.
    /// </summary>
    internal static class JsonSerializer
    {
        internal static string Serialize(object? obj, int? indent = null, bool sortKeys = false, bool ensureAscii = false)
        {
            var sb = new StringBuilder();
            SerializeValue(sb, obj, indent, sortKeys, ensureAscii, 0);
            return sb.ToString();
        }

        private static void SerializeValue(StringBuilder sb, object? obj, int? indent, bool sortKeys, bool ensureAscii, int depth)
        {
            if (obj == null)
            {
                sb.Append("null");
            }
            else if (obj is bool b)
            {
                // Must check bool before int since bool is convertible to int in .NET
                sb.Append(b ? "true" : "false");
            }
            else if (obj is string s)
            {
                SerializeString(sb, s, ensureAscii);
            }
            else if (obj is int i)
            {
                sb.Append(i.ToString(CultureInfo.InvariantCulture));
            }
            else if (obj is long l)
            {
                sb.Append(l.ToString(CultureInfo.InvariantCulture));
            }
            else if (obj is double d)
            {
                if (double.IsInfinity(d) || double.IsNaN(d))
                {
                    throw new ValueError("Out of range float values are not JSON compliant");
                }
                sb.Append(FormatDouble(d));
            }
            else if (obj is float f)
            {
                if (float.IsInfinity(f) || float.IsNaN(f))
                {
                    throw new ValueError("Out of range float values are not JSON compliant");
                }
                sb.Append(FormatDouble(f));
            }
            else if (IsDictLike(obj))
            {
                SerializeDict(sb, obj, indent, sortKeys, ensureAscii, depth);
            }
            else if (IsListLike(obj))
            {
                SerializeList(sb, obj, indent, sortKeys, ensureAscii, depth);
            }
            else
            {
                throw new TypeError("Object of type '" + obj.GetType().Name + "' is not JSON serializable");
            }
        }

        private static string FormatDouble(double d)
        {
            string s = d.ToString("R", CultureInfo.InvariantCulture);
            if (s.IndexOf('.') < 0 && s.IndexOf('E') < 0 && s.IndexOf('e') < 0)
            {
                s += ".0";
            }
            return s;
        }

        private static bool IsDictLike(object obj)
        {
            if (obj is IDictionary)
                return true;
            if (obj is Dict<string, object?>)
                return true;
            // Check for generic IDictionary<K,V>
            var type = obj.GetType();
            foreach (var iface in type.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                    return true;
            }
            return false;
        }

        private static bool IsListLike(object obj)
        {
            if (obj is string)
                return false;
            return obj is IList || obj is IEnumerable;
        }

        private static void SerializeDict(StringBuilder sb, object obj, int? indent, bool sortKeys, bool ensureAscii, int depth)
        {
            sb.Append('{');

            var entries = new System.Collections.Generic.List<KeyValuePair<string, object?>>();

            if (obj is Dict<string, object?> sharpyDict)
            {
                foreach (string key in sharpyDict.Keys())
                {
                    entries.Add(new KeyValuePair<string, object?>(key, sharpyDict[key]));
                }
            }
            else if (obj is IDictionary nonGenericDict)
            {
                foreach (DictionaryEntry entry in nonGenericDict)
                {
                    if (!(entry.Key is string key))
                    {
                        throw new TypeError("keys must be str, not " + entry.Key.GetType().Name);
                    }
                    entries.Add(new KeyValuePair<string, object?>(key, entry.Value));
                }
            }
            else
            {
                // Handle generic IDictionary<K,V> via IEnumerable
                foreach (var item in (IEnumerable)obj)
                {
                    var itemType = item.GetType();
                    var keyProp = itemType.GetProperty("Key");
                    var valProp = itemType.GetProperty("Value");
                    if (keyProp != null && valProp != null)
                    {
                        var key = keyProp.GetValue(item);
                        if (!(key is string keyStr))
                        {
                            throw new TypeError("keys must be str, not " + (key?.GetType().Name ?? "None"));
                        }
                        entries.Add(new KeyValuePair<string, object?>(keyStr, valProp.GetValue(item)));
                    }
                }
            }

            if (sortKeys)
            {
                entries.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                if (indent.HasValue)
                {
                    sb.Append('\n');
                    sb.Append(new string(' ', indent.Value * (depth + 1)));
                }
                else
                {
                    if (i > 0)
                    {
                        sb.Append(' ');
                    }
                }

                SerializeString(sb, entries[i].Key, ensureAscii);
                sb.Append(": ");
                SerializeValue(sb, entries[i].Value, indent, sortKeys, ensureAscii, depth + 1);
            }

            if (entries.Count > 0 && indent.HasValue)
            {
                sb.Append('\n');
                sb.Append(new string(' ', indent.Value * depth));
            }
            sb.Append('}');
        }

        private static void SerializeList(StringBuilder sb, object obj, int? indent, bool sortKeys, bool ensureAscii, int depth)
        {
            sb.Append('[');

            var items = new System.Collections.Generic.List<object?>();
            if (obj is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    items.Add(item);
                }
            }

            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                if (indent.HasValue)
                {
                    sb.Append('\n');
                    sb.Append(new string(' ', indent.Value * (depth + 1)));
                }
                else
                {
                    if (i > 0)
                    {
                        sb.Append(' ');
                    }
                }

                SerializeValue(sb, items[i], indent, sortKeys, ensureAscii, depth + 1);
            }

            if (items.Count > 0 && indent.HasValue)
            {
                sb.Append('\n');
                sb.Append(new string(' ', indent.Value * depth));
            }
            sb.Append(']');
        }

        private static void SerializeString(StringBuilder sb, string s, bool ensureAscii)
        {
            sb.Append('"');
            foreach (char c in s)
            {
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
                        if (c < 0x20)
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4"));
                        }
                        else if (ensureAscii && c > 0x7F)
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4"));
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
    }
}
