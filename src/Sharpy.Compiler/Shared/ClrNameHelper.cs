using System;

namespace Sharpy.Compiler.Shared
{
    /// <summary>
    /// Utilities for working with CLR type names, particularly
    /// stripping generic arity suffixes (e.g., <c>List`1</c> → <c>List</c>).
    /// </summary>
    internal static class ClrNameHelper
    {
        /// <summary>
        /// Strips the CLR generic arity suffix from a type name or fully-qualified name.
        /// Returns the name unchanged if no backtick is present.
        /// </summary>
        /// <param name="name">
        /// A CLR type name (e.g., <c>"List`1"</c>) or fully-qualified name
        /// (e.g., <c>"Sharpy.DefaultDict`2"</c>).
        /// </param>
        /// <returns>The name with the backtick and arity digits removed.</returns>
        internal static string StripArity(string name)
        {
            var idx = name.IndexOf('`', StringComparison.Ordinal);
            return idx >= 0 ? name[..idx] : name;
        }
    }
}
