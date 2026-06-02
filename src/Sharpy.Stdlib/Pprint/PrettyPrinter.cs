#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Sharpy
{
    [SharpyModuleType("pprint", "PrettyPrinter")]
    public sealed class PrettyPrinter
    {
        private readonly int _indent;
        private readonly int _width;
        private readonly int? _depth;
        private readonly bool _compact;
        private readonly bool _sortDicts;

        public PrettyPrinter(int indent = 1, int width = 80, int? depth = null, bool compact = false, bool sortDicts = true)
        {
            if (indent < 0)
                throw new ValueError("indent must be >= 0");
            if (width < 1)
                throw new ValueError("width must be >= 1");

            _indent = indent;
            _width = width;
            _depth = depth;
            _compact = compact;
            _sortDicts = sortDicts;
        }

        public void Pprint(object? obj)
        {
            Console.WriteLine(Pformat(obj));
        }

        public string Pformat(object? obj)
        {
            var sb = new StringBuilder();
            var seen = new HashSet<int>();
            FormatObject(sb, obj, 0, 0, seen);
            return sb.ToString();
        }

        public bool Isreadable(object? obj)
        {
            if (Isrecursive(obj))
                return false;
            return IsReadableValue(obj, new HashSet<int>(), 0);
        }

        public bool Isrecursive(object? obj)
        {
            return HasCircularReference(obj, new HashSet<int>());
        }

        private static bool IsGenericDict(object obj)
        {
            foreach (var iface in obj.GetType().GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                    return true;
            }
            return false;
        }

        private static bool IsGenericList(object obj)
        {
            foreach (var iface in obj.GetType().GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IList<>))
                    return true;
            }
            return false;
        }

        private static bool IsGenericSet(object obj)
        {
            foreach (var iface in obj.GetType().GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(ISet<>))
                    return true;
            }
            return false;
        }

        private static bool IsAnyCollection(object obj)
        {
            return obj is IDictionary || obj is IList || obj is ICollection
                || IsGenericDict(obj) || IsGenericList(obj) || IsGenericSet(obj);
        }

        private static int GetCollectionCount(object obj)
        {
            if (obj is ICollection col)
                return col.Count;

            foreach (var iface in obj.GetType().GetInterfaces())
            {
                if (iface.IsGenericType)
                {
                    var def = iface.GetGenericTypeDefinition();
                    if (def == typeof(ICollection<>) || def == typeof(IReadOnlyCollection<>))
                        return (int)iface.GetProperty("Count")!.GetValue(obj)!;
                }
            }
            return 0;
        }

        private static object? GetListItem(object obj, int index)
        {
            if (obj is IList list)
                return list[index];

            foreach (var iface in obj.GetType().GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IList<>))
                    return iface.GetProperty("Item")!.GetValue(obj, new object[] { index });
            }
            return null;
        }

        private static System.Collections.Generic.List<object?> GetDictKeys(object obj)
        {
            var keys = new System.Collections.Generic.List<object?>();
            if (obj is IDictionary dict)
            {
                foreach (var key in dict.Keys)
                    keys.Add(key);
                return keys;
            }

            foreach (var iface in obj.GetType().GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                {
                    foreach (var key in (IEnumerable)iface.GetProperty("Keys")!.GetValue(obj)!)
                        keys.Add(key);
                    return keys;
                }
            }
            return keys;
        }

        private static object? GetDictValue(object obj, object? key)
        {
            if (obj is IDictionary dict)
                return dict[key!];

            foreach (var iface in obj.GetType().GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                    return iface.GetProperty("Item")!.GetValue(obj, new[] { key });
            }
            return null;
        }

        private void FormatObject(StringBuilder sb, object? obj, int currentIndent, int currentDepth, HashSet<int> seen)
        {
            if (obj == null)
            {
                sb.Append("None");
                return;
            }

            if (obj is bool boolVal)
            {
                sb.Append(boolVal ? "True" : "False");
                return;
            }

            if (obj is int || obj is long || obj is short || obj is byte)
            {
                sb.Append(obj.ToString());
                return;
            }

            if (obj is float f)
            {
                sb.Append(FormatFloat(f));
                return;
            }

            if (obj is double d)
            {
                sb.Append(FormatFloat(d));
                return;
            }

            if (obj is string s)
            {
                sb.Append(Builtins.Repr(s));
                return;
            }

            if (IsValueTuple(obj))
            {
                if (_depth.HasValue && currentDepth >= _depth.Value)
                {
                    sb.Append("...");
                    return;
                }
                FormatTuple(sb, obj, currentIndent, currentDepth, seen);
                return;
            }

            int id = RuntimeHelpers.GetHashCode(obj);

            if (IsAnyCollection(obj))
            {
                if (seen.Contains(id))
                {
                    sb.Append("<Recursion on ");
                    sb.Append(GetTypeName(obj));
                    sb.Append(" with id=");
                    sb.Append(id);
                    sb.Append('>');
                    return;
                }

                if (_depth.HasValue && currentDepth >= _depth.Value)
                {
                    sb.Append("...");
                    return;
                }

                seen.Add(id);
            }

            try
            {
                if (obj is IDictionary || IsGenericDict(obj))
                    FormatDict(sb, obj, currentIndent, currentDepth, seen);
                else if (obj is IList || IsGenericList(obj))
                    FormatList(sb, obj, currentIndent, currentDepth, seen);
                else if (obj is ICollection || IsGenericSet(obj))
                    FormatSet(sb, obj, currentIndent, currentDepth, seen);
                else
                    sb.Append(Builtins.Repr(obj));
            }
            finally
            {
                if (IsAnyCollection(obj))
                    seen.Remove(id);
            }
        }

        private void FormatDict(StringBuilder sb, object obj, int currentIndent, int currentDepth, HashSet<int> seen)
        {
            if (GetCollectionCount(obj) == 0)
            {
                sb.Append("{}");
                return;
            }

            string singleLine = FormatDictSingleLine(obj, currentDepth, new HashSet<int>(seen));
            if (singleLine.Length + currentIndent <= _width)
            {
                sb.Append(singleLine);
                return;
            }

            int childIndent = currentIndent + _indent;
            string indentStr = new string(' ', childIndent);

            sb.Append('{');
            bool first = true;

            var keys = GetSortedDictKeys(obj);
            foreach (var key in keys)
            {
                if (!first)
                    sb.Append(',');
                sb.Append('\n');
                sb.Append(indentStr);
                FormatObject(sb, key, childIndent, currentDepth + 1, seen);
                sb.Append(": ");
                FormatObject(sb, GetDictValue(obj, key), childIndent + GetFormattedLength(key) + 2, currentDepth + 1, seen);
                first = false;
            }

            sb.Append('}');
        }

        private void FormatList(StringBuilder sb, object obj, int currentIndent, int currentDepth, HashSet<int> seen)
        {
            FormatSequence(sb, obj, currentIndent, currentDepth, seen, '[', ']');
        }

        private void FormatTuple(StringBuilder sb, object tuple, int currentIndent, int currentDepth, HashSet<int> seen)
        {
            var items = GetValueTupleItems(tuple);
            if (items.Length == 0)
            {
                sb.Append("()");
                return;
            }

            string singleLine = FormatTupleSingleLine(items, currentDepth, new HashSet<int>(seen));
            if (singleLine.Length + currentIndent <= _width)
            {
                sb.Append(singleLine);
                return;
            }

            int childIndent = currentIndent + _indent;
            string indentStr = new string(' ', childIndent);

            sb.Append('(');
            for (int i = 0; i < items.Length; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append('\n');
                sb.Append(indentStr);
                FormatObject(sb, items[i], childIndent, currentDepth + 1, seen);
            }

            if (items.Length == 1)
                sb.Append(',');
            sb.Append(')');
        }

        private void FormatSet(StringBuilder sb, object obj, int currentIndent, int currentDepth, HashSet<int> seen)
        {
            if (GetCollectionCount(obj) == 0)
            {
                sb.Append("set()");
                return;
            }

            string singleLine = FormatSetSingleLine(obj, currentDepth, new HashSet<int>(seen));
            if (singleLine.Length + currentIndent <= _width)
            {
                sb.Append(singleLine);
                return;
            }

            int childIndent = currentIndent + _indent;
            string indentStr = new string(' ', childIndent);

            sb.Append('{');
            bool first = true;
            foreach (var item in (IEnumerable)obj)
            {
                if (!first)
                    sb.Append(',');
                sb.Append('\n');
                sb.Append(indentStr);
                FormatObject(sb, item, childIndent, currentDepth + 1, seen);
                first = false;
            }

            sb.Append('}');
        }

        private void FormatSequence(StringBuilder sb, object obj, int currentIndent, int currentDepth, HashSet<int> seen, char open, char close)
        {
            int count = GetCollectionCount(obj);
            if (count == 0)
            {
                sb.Append(open);
                sb.Append(close);
                return;
            }

            string singleLine = FormatSequenceSingleLine(obj, count, currentDepth, new HashSet<int>(seen), open, close);
            if (singleLine.Length + currentIndent <= _width)
            {
                sb.Append(singleLine);
                return;
            }

            int childIndent = currentIndent + _indent;
            string indentStr = new string(' ', childIndent);

            sb.Append(open);

            if (_compact)
            {
                FormatCompactSequence(sb, obj, count, childIndent, currentDepth, seen, indentStr);
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    if (i > 0)
                        sb.Append(',');
                    sb.Append('\n');
                    sb.Append(indentStr);
                    FormatObject(sb, GetListItem(obj, i), childIndent, currentDepth + 1, seen);
                }
            }

            sb.Append(close);
        }

        private void FormatCompactSequence(StringBuilder sb, object obj, int count, int childIndent, int currentDepth, HashSet<int> seen, string indentStr)
        {
            sb.Append('\n');
            sb.Append(indentStr);
            int lineLength = childIndent;
            bool firstOnLine = true;

            for (int i = 0; i < count; i++)
            {
                var itemSb = new StringBuilder();
                FormatObject(itemSb, GetListItem(obj, i), childIndent, currentDepth + 1, seen);
                string itemStr = itemSb.ToString();

                int itemLength = firstOnLine ? itemStr.Length : itemStr.Length + 2;
                if (!firstOnLine && lineLength + itemLength > _width)
                {
                    sb.Append(',');
                    sb.Append('\n');
                    sb.Append(indentStr);
                    lineLength = childIndent;
                    firstOnLine = true;
                }

                if (!firstOnLine)
                {
                    sb.Append(", ");
                    lineLength += 2;
                }

                sb.Append(itemStr);
                lineLength += itemStr.Length;
                firstOnLine = false;
            }
        }

        private string FormatDictSingleLine(object obj, int currentDepth, HashSet<int> seen)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            bool first = true;
            var keys = GetSortedDictKeys(obj);
            foreach (var key in keys)
            {
                if (!first)
                    sb.Append(", ");
                FormatObject(sb, key, 0, currentDepth + 1, seen);
                sb.Append(": ");
                FormatObject(sb, GetDictValue(obj, key), 0, currentDepth + 1, seen);
                first = false;
            }
            sb.Append('}');
            return sb.ToString();
        }

        private string FormatSequenceSingleLine(object obj, int count, int currentDepth, HashSet<int> seen, char open, char close)
        {
            var sb = new StringBuilder();
            sb.Append(open);
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                FormatObject(sb, GetListItem(obj, i), 0, currentDepth + 1, seen);
            }
            sb.Append(close);
            return sb.ToString();
        }

        private string FormatTupleSingleLine(object?[] items, int currentDepth, HashSet<int> seen)
        {
            var sb = new StringBuilder();
            sb.Append('(');
            for (int i = 0; i < items.Length; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                FormatObject(sb, items[i], 0, currentDepth + 1, seen);
            }
            if (items.Length == 1)
                sb.Append(',');
            sb.Append(')');
            return sb.ToString();
        }

        private string FormatSetSingleLine(object obj, int currentDepth, HashSet<int> seen)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            bool first = true;
            foreach (var item in (IEnumerable)obj)
            {
                if (!first)
                    sb.Append(", ");
                FormatObject(sb, item, 0, currentDepth + 1, seen);
                first = false;
            }
            sb.Append('}');
            return sb.ToString();
        }

        private System.Collections.Generic.List<object?> GetSortedDictKeys(object obj)
        {
            var keys = GetDictKeys(obj);
            if (_sortDicts)
                keys.Sort(CompareObjects);
            return keys;
        }

        private static int CompareObjects(object? a, object? b)
        {
            if (a == null && b == null)
                return 0;
            if (a == null)
                return -1;
            if (b == null)
                return 1;
            if (a is IComparable ca)
            {
                try
                { return ca.CompareTo(b); }
                catch (ArgumentException) { }
            }
            return string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal);
        }

        private static string FormatFloat(double d)
        {
            if (double.IsPositiveInfinity(d))
                return "inf";
            if (double.IsNegativeInfinity(d))
                return "-inf";
            if (double.IsNaN(d))
                return "nan";

            string result = d.ToString("G");
            if (!result.Contains('.') && !result.Contains('E') && !result.Contains('e'))
                result += ".0";
            return result;
        }

        private static string FormatFloat(float f) => FormatFloat((double)f);

        private static string GetTypeName(object obj)
        {
            if (obj is IDictionary || IsGenericDict(obj))
                return "dict";
            if (obj is IList || IsGenericList(obj))
                return "list";
            if (IsGenericSet(obj))
                return "set";
            return obj.GetType().Name;
        }

        private int GetFormattedLength(object? obj)
        {
            if (obj == null)
                return 4;
            if (obj is string s)
                return Builtins.Repr(s).Length;
            if (obj is bool b)
                return b ? 4 : 5;
            return obj.ToString()?.Length ?? 0;
        }

        private static bool IsValueTuple(object? obj)
        {
            if (obj == null)
                return false;
            var type = obj.GetType();
            return type.IsValueType && type.FullName != null
                && type.FullName.StartsWith("System.ValueTuple`", StringComparison.Ordinal);
        }

        private static object?[] GetValueTupleItems(object tuple)
        {
            var type = tuple.GetType();
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            var items = new object?[fields.Length];
            for (int i = 0; i < fields.Length; i++)
                items[i] = fields[i].GetValue(tuple);
            return items;
        }

        private bool HasCircularReference(object? obj, HashSet<int> seen)
        {
            if (obj == null || obj is string || obj is bool ||
                obj is int || obj is long || obj is float || obj is double)
                return false;

            int id = RuntimeHelpers.GetHashCode(obj);
            if (seen.Contains(id))
                return true;

            seen.Add(id);
            try
            {
                if (obj is IDictionary dict)
                {
                    foreach (DictionaryEntry entry in dict)
                    {
                        if (HasCircularReference(entry.Key, seen) || HasCircularReference(entry.Value, seen))
                            return true;
                    }
                }
                else if (IsGenericDict(obj))
                {
                    foreach (var entry in (IEnumerable)obj)
                    {
                        var entryType = entry.GetType();
                        var key = entryType.GetProperty("Key")!.GetValue(entry);
                        var value = entryType.GetProperty("Value")!.GetValue(entry);
                        if (HasCircularReference(key, seen) || HasCircularReference(value, seen))
                            return true;
                    }
                }
                else if (obj is IList list)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (HasCircularReference(list[i], seen))
                            return true;
                    }
                }
                else if (obj is IEnumerable enumerable && !(obj is string))
                {
                    foreach (var item in enumerable)
                    {
                        if (HasCircularReference(item, seen))
                            return true;
                    }
                }
            }
            finally
            {
                seen.Remove(id);
            }
            return false;
        }

        private bool IsReadableValue(object? obj, HashSet<int> seen, int depth)
        {
            if (obj == null || obj is string || obj is bool ||
                obj is int || obj is long || obj is float || obj is double)
                return true;

            if (obj is IDictionary dict)
            {
                foreach (DictionaryEntry entry in dict)
                {
                    if (!IsReadableValue(entry.Key, seen, depth + 1) || !IsReadableValue(entry.Value, seen, depth + 1))
                        return false;
                }
                return true;
            }

            if (IsGenericDict(obj))
            {
                foreach (var entry in (IEnumerable)obj)
                {
                    var entryType = entry.GetType();
                    var key = entryType.GetProperty("Key")!.GetValue(entry);
                    var value = entryType.GetProperty("Value")!.GetValue(entry);
                    if (!IsReadableValue(key, seen, depth + 1) || !IsReadableValue(value, seen, depth + 1))
                        return false;
                }
                return true;
            }

            if (obj is IList list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (!IsReadableValue(list[i], seen, depth + 1))
                        return false;
                }
                return true;
            }

            if (IsGenericList(obj) || IsGenericSet(obj))
            {
                foreach (var item in (IEnumerable)obj)
                {
                    if (!IsReadableValue(item, seen, depth + 1))
                        return false;
                }
                return true;
            }

            if (IsValueTuple(obj))
            {
                foreach (var item in GetValueTupleItems(obj))
                {
                    if (!IsReadableValue(item, seen, depth + 1))
                        return false;
                }
                return true;
            }

            if (obj is ICollection)
            {
                foreach (var item in (IEnumerable)obj)
                {
                    if (!IsReadableValue(item, seen, depth + 1))
                        return false;
                }
                return true;
            }

            return false;
        }
    }
}
