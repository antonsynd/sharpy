using System.Reflection;
using System.Text;

namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Return a string containing a printable representation of an object.
        /// </summary>
        /// <remarks>
        /// Uses <see cref="object.ToString()"/> to get the representation.
        /// Sharpy types (List, Set, Dict) override ToString() to produce
        /// Python-compatible repr output (e.g., "[1, 2, 3]", "{1, 2}", etc.).
        /// Strings are wrapped in single quotes, matching Python's repr().
        /// </remarks>
        /// <param name="obj">The object to get the representation of</param>
        /// <returns>A printable string representation</returns>
        /// <example>
        /// <code>
        /// repr("hello")      # "'hello'"
        /// repr([1, 2, 3])    # "[1, 2, 3]"
        /// repr(None)         # "None"
        /// </code>
        /// </example>
        public static string Repr(object? obj)
        {
            if (obj == null)
                return "None";

            if (obj is string s)
                return "'" + s + "'";

            if (obj is bool b)
                return b ? "True" : "False";

            // Handle ValueTuples (System.ValueTuple<...>)
            var type = obj.GetType();
            if (type.IsValueType && type.FullName != null
                && type.FullName.StartsWith("System.ValueTuple`", System.StringComparison.Ordinal))
            {
                return FormatValueTuple(obj, type);
            }

            return obj.ToString() ?? "None";
        }

        private static string FormatValueTuple(object tuple, System.Type type)
        {
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            var builder = new StringBuilder();
            builder.Append('(');
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0)
                    builder.Append(", ");
                builder.Append(Repr(fields[i].GetValue(tuple)));
            }
            builder.Append(')');
            return builder.ToString();
        }
    }
}
