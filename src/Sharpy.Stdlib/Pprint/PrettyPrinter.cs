using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// A class that handles pretty-printing of data structures.
    /// Provides configurable formatting with indent, width, depth, compact, and sort_dicts options.
    /// </summary>
    [SharpyModuleType("pprint")]
    public sealed class PrettyPrinter
    {
        private readonly int _indent;
        private readonly int _width;
        private readonly int? _depth;
        private readonly bool _compact;
        private readonly bool _sortDicts;

        /// <summary>
        /// Create a new PrettyPrinter instance.
        /// </summary>
        /// <param name="indent">Number of spaces added for each nesting level.</param>
        /// <param name="width">Maximum number of characters per output line.</param>
        /// <param name="depth">Maximum nesting depth to print. None means unlimited.</param>
        /// <param name="compact">If true, fit as many items as possible on each output line.</param>
        /// <param name="sortDicts">If true, dict output is sorted by key.</param>
        public PrettyPrinter(int indent = 1, int width = 80, int? depth = null, bool compact = false, bool sortDicts = true)
        {
            if (indent < 0)
            {
                throw new global::Sharpy.ValueError("indent must be >= 0");
            }

            if (width < 1)
            {
                throw new global::Sharpy.ValueError("width must be >= 1");
            }

            _indent = indent;
            _width = width;
            _depth = depth;
            _compact = compact;
            _sortDicts = sortDicts;
        }

        /// <summary>
        /// Pretty-print an object to stdout.
        /// </summary>
        /// <param name="obj">The object to pretty-print.</param>
        public void Pprint(object? obj)
        {
            string output = Pformat(obj);
            Console.WriteLine(output);
        }

        /// <summary>
        /// Format an object into a pretty-printed string.
        /// </summary>
        /// <param name="obj">The object to format.</param>
        /// <returns>A pretty-printed string representation.</returns>
        public string Pformat(object? obj)
        {
            var sb = new StringBuilder();
            var seen = new HashSet<int>();
            FormatObject(sb, obj, 0, 0, seen);
            return sb.ToString();
        }

        /// <summary>
        /// Determine whether the object can be pretty-printed without encountering
        /// objects that cannot be represented as a valid Python literal.
        /// </summary>
        /// <param name="obj">The object to check.</param>
        /// <returns>True if the object is readable.</returns>
        public bool Isreadable(object? obj)
        {
            if (Isrecursive(obj))
            {
                return false;
            }

            return IsReadableValue(obj, new HashSet<int>(), 0);
        }

        /// <summary>
        /// Determine whether the object requires recursive representation.
        /// </summary>
        /// <param name="obj">The object to check.</param>
        /// <returns>True if the object contains circular references.</returns>
        public bool Isrecursive(object? obj)
        {
            return HasCircularReference(obj, new HashSet<int>());
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
                sb.Append(FormatString(s));
                return;
            }

            // Handle ValueTuples before collection checks (value types can't be circular)
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

            // Check for circular references in collections
            int id = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);

            if (obj is IDictionary || obj is IList || obj is ICollection)
            {
                if (seen.Contains(id))
                {
                    sb.Append("<Recursion on ");
                    sb.Append(GetTypeName(obj));
                    sb.Append(">");
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
                if (obj is IDictionary dict)
                {
                    FormatDict(sb, dict, currentIndent, currentDepth, seen);
                }
                else if (obj is IList list)
                {
                    FormatList(sb, list, currentIndent, currentDepth, seen);
                }
                else if (obj is ICollection set)
                {
                    FormatSet(sb, set, currentIndent, currentDepth, seen);
                }
                else
                {
                    sb.Append(Builtins.Repr(obj));
                }
            }
            finally
            {
                if (obj is IDictionary || obj is IList || obj is ICollection)
                {
                    seen.Remove(id);
                }
            }
        }

        private void FormatDict(StringBuilder sb, IDictionary dict, int currentIndent, int currentDepth, HashSet<int> seen)
        {
            if (dict.Count == 0)
            {
                sb.Append("{}");
                return;
            }

            // Try single-line first
            string singleLine = FormatDictSingleLine(dict, currentDepth, new HashSet<int>(seen));
            if (singleLine.Length + currentIndent <= _width)
            {
                sb.Append(singleLine);
                return;
            }

            // Multi-line format
            int childIndent = currentIndent + _indent;
            string indentStr = new string(' ', childIndent);

            sb.Append('{');
            bool first = true;

            var keys = GetDictKeys(dict);
            foreach (var key in keys)
            {
                if (!first)
                {
                    sb.Append(',');
                }

                sb.Append('\n');
                sb.Append(indentStr);
                FormatObject(sb, key, childIndent, currentDepth + 1, seen);
                sb.Append(": ");
                FormatObject(sb, dict[key!], childIndent + GetFormattedLength(key) + 2, currentDepth + 1, seen);
                first = false;
            }

            sb.Append('}');
        }

        private void FormatList(StringBuilder sb, IList list, int currentIndent, int currentDepth, HashSet<int> seen)
        {
            FormatSequence(sb, list, currentIndent, currentDepth, seen, '[', ']');
        }

        private void FormatTuple(StringBuilder sb, object tuple, int currentIndent, int currentDepth, HashSet<int> seen)
        {
            var items = GetValueTupleItems(tuple);
            if (items.Length == 0)
            {
                sb.Append("()");
                return;
            }

            // Try single-line first
            string singleLine = FormatTupleSingleLine(items, currentDepth, new HashSet<int>(seen));
            if (singleLine.Length + currentIndent <= _width)
            {
                sb.Append(singleLine);
                return;
            }

            // Multi-line
            int childIndent = currentIndent + _indent;
            string indentStr = new string(' ', childIndent);

            sb.Append('(');
            for (int i = 0; i < items.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append('\n');
                sb.Append(indentStr);
                FormatObject(sb, items[i], childIndent, currentDepth + 1, seen);
            }

            if (items.Length == 1)
            {
                sb.Append(',');
            }

            sb.Append(')');
        }

        private void FormatSet(StringBuilder sb, ICollection set, int currentIndent, int currentDepth, HashSet<int> seen)
        {
            if (GetCollectionCount(set) == 0)
            {
                sb.Append("set()");
                return;
            }

            // Try single-line first
            string singleLine = FormatSetSingleLine(set, currentDepth, new HashSet<int>(seen));
            if (singleLine.Length + currentIndent <= _width)
            {
                sb.Append(singleLine);
                return;
            }

            // Multi-line
            int childIndent = currentIndent + _indent;
            string indentStr = new string(' ', childIndent);

            sb.Append('{');
            bool first = true;
            foreach (var item in (IEnumerable)set)
            {
                if (!first)
                {
                    sb.Append(',');
                }

                sb.Append('\n');
                sb.Append(indentStr);
                FormatObject(sb, item, childIndent, currentDepth + 1, seen);
                first = false;
            }

            sb.Append('}');
        }

        private void FormatSequence(StringBuilder sb, IList list, int currentIndent, int currentDepth, HashSet<int> seen, char open, char close)
        {
            if (list.Count == 0)
            {
                sb.Append(open);
                sb.Append(close);
                return;
            }

            // Try single-line first
            string singleLine = FormatSequenceSingleLine(list, currentDepth, new HashSet<int>(seen), open, close);
            if (singleLine.Length + currentIndent <= _width)
            {
                sb.Append(singleLine);
                return;
            }

            // Multi-line format
            int childIndent = currentIndent + _indent;
            string indentStr = new string(' ', childIndent);

            sb.Append(open);

            if (_compact)
            {
                FormatCompactSequence(sb, list, currentIndent, childIndent, currentDepth, seen, indentStr);
            }
            else
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(',');
                    }

                    sb.Append('\n');
                    sb.Append(indentStr);
                    FormatObject(sb, list[i], childIndent, currentDepth + 1, seen);
                }
            }

            sb.Append(close);
        }

        private void FormatCompactSequence(StringBuilder sb, IList list, int currentIndent, int childIndent, int currentDepth, HashSet<int> seen, string indentStr)
        {
            sb.Append('\n');
            sb.Append(indentStr);
            int lineLength = childIndent;
            bool firstOnLine = true;

            for (int i = 0; i < list.Count; i++)
            {
                var itemSb = new StringBuilder();
                FormatObject(itemSb, list[i], childIndent, currentDepth + 1, seen);
                string itemStr = itemSb.ToString();

                int itemLength = firstOnLine ? itemStr.Length : itemStr.Length + 2; // +2 for ", "
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

        private string FormatDictSingleLine(IDictionary dict, int currentDepth, HashSet<int> seen)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            bool first = true;
            var keys = GetDictKeys(dict);
            foreach (var key in keys)
            {
                if (!first)
                {
                    sb.Append(", ");
                }

                FormatObject(sb, key, 0, currentDepth + 1, seen);
                sb.Append(": ");
                FormatObject(sb, dict[key!], 0, currentDepth + 1, seen);
                first = false;
            }

            sb.Append('}');
            return sb.ToString();
        }

        private string FormatSequenceSingleLine(IList list, int currentDepth, HashSet<int> seen, char open, char close)
        {
            var sb = new StringBuilder();
            sb.Append(open);
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                FormatObject(sb, list[i], 0, currentDepth + 1, seen);
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
                {
                    sb.Append(", ");
                }

                FormatObject(sb, items[i], 0, currentDepth + 1, seen);
            }

            if (items.Length == 1)
            {
                sb.Append(',');
            }

            sb.Append(')');
            return sb.ToString();
        }

        private string FormatSetSingleLine(ICollection set, int currentDepth, HashSet<int> seen)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            bool first = true;
            foreach (var item in (IEnumerable)set)
            {
                if (!first)
                {
                    sb.Append(", ");
                }

                FormatObject(sb, item, 0, currentDepth + 1, seen);
                first = false;
            }

            sb.Append('}');
            return sb.ToString();
        }

        private IList<object?> GetDictKeys(IDictionary dict)
        {
            var keys = new System.Collections.Generic.List<object?>();
            foreach (var key in dict.Keys)
            {
                keys.Add(key);
            }

            if (_sortDicts)
            {
                keys.Sort(CompareObjects);
            }

            return keys;
        }

        private static int CompareObjects(object? a, object? b)
        {
            if (a == null && b == null) return 0;
            if (a == null) return -1;
            if (b == null) return 1;
            if (a is IComparable ca)
            {
                try
                {
                    return ca.CompareTo(b);
                }
                catch (ArgumentException)
                {
                    return string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal);
                }
            }

            return string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal);
        }

        private static string FormatString(string s)
        {
            return Builtins.Repr(s);
        }

        private static string FormatFloat(double d)
        {
            if (double.IsPositiveInfinity(d)) return "inf";
            if (double.IsNegativeInfinity(d)) return "-inf";
            if (double.IsNaN(d)) return "nan";

            string result = d.ToString("G");
            if (!result.Contains('.') && !result.Contains('E') && !result.Contains('e'))
            {
                result += ".0";
            }

            return result;
        }

        private static string FormatFloat(float f)
        {
            return FormatFloat((double)f);
        }

        private static string GetTypeName(object obj)
        {
            if (obj is IDictionary) return "dict";
            if (obj is IList) return "list";
            return obj.GetType().Name;
        }

        private static int GetCollectionCount(ICollection col)
        {
            return col.Count;
        }

        private int GetFormattedLength(object? obj)
        {
            if (obj == null) return 4; // "None"
            if (obj is string s) return FormatString(s).Length;
            if (obj is bool b) return b ? 4 : 5;
            return obj.ToString()?.Length ?? 0;
        }

        private static bool IsValueTuple(object? obj)
        {
            if (obj == null) return false;
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
            {
                items[i] = fields[i].GetValue(tuple);
            }

            return items;
        }

        private bool HasCircularReference(object? obj, HashSet<int> seen)
        {
            if (obj == null || obj is string || obj is bool ||
                obj is int || obj is long || obj is float || obj is double)
            {
                return false;
            }

            int id = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            if (seen.Contains(id))
            {
                return true;
            }

            seen.Add(id);
            try
            {
                if (obj is IDictionary dict)
                {
                    foreach (DictionaryEntry entry in dict)
                    {
                        if (HasCircularReference(entry.Key, seen) || HasCircularReference(entry.Value, seen))
                        {
                            return true;
                        }
                    }
                }
                else if (obj is IList list)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (HasCircularReference(list[i], seen))
                        {
                            return true;
                        }
                    }
                }
                else if (obj is IEnumerable enumerable && !(obj is string))
                {
                    foreach (var item in enumerable)
                    {
                        if (HasCircularReference(item, seen))
                        {
                            return true;
                        }
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
            {
                return true;
            }

            if (obj is IDictionary dict)
            {
                foreach (DictionaryEntry entry in dict)
                {
                    if (!IsReadableValue(entry.Key, seen, depth + 1) || !IsReadableValue(entry.Value, seen, depth + 1))
                    {
                        return false;
                    }
                }

                return true;
            }

            if (obj is IList list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (!IsReadableValue(list[i], seen, depth + 1))
                    {
                        return false;
                    }
                }

                return true;
            }

            if (IsValueTuple(obj))
            {
                foreach (var item in GetValueTupleItems(obj))
                {
                    if (!IsReadableValue(item, seen, depth + 1))
                    {
                        return false;
                    }
                }

                return true;
            }

            if (obj is ICollection)
            {
                foreach (var item in (IEnumerable)obj)
                {
                    if (!IsReadableValue(item, seen, depth + 1))
                    {
                        return false;
                    }
                }

                return true;
            }

            // Unknown types are not readable
            return false;
        }
    }
}
