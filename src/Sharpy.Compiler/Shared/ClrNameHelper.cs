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

        /// <summary>
        /// Converts a CLR <see cref="System.Type.FullName"/> into a valid C# qualified name:
        /// strips every generic arity suffix (<c>`N</c>) and converts the reflection
        /// nested-type separator (<c>+</c>) into <c>.</c>. For example,
        /// <c>"Sharpy.SocketModule+Socket"</c> → <c>"Sharpy.SocketModule.Socket"</c> and
        /// <c>"Outer`1+Inner"</c> → <c>"Outer.Inner"</c>.
        /// </summary>
        internal static string ToCSharpQualifiedName(string fullName)
        {
            return StripAllArity(fullName).Replace('+', '.');
        }

        /// <summary>
        /// Reduces a CLR type name to its assembly-independent full name (namespace +
        /// nested-type path, using <c>+</c> for nesting and <c>`N</c> arity suffixes intact).
        /// Accepts either an assembly-qualified name (e.g.
        /// <c>"Sharpy.SocketModule+Error, Sharpy.Stdlib, Version=..."</c>) or a plain
        /// <see cref="System.Type.FullName"/> (e.g. <c>"Sharpy.SocketModule+Error"</c>) and
        /// returns the same string for both: the part before the first top-level comma.
        /// This yields a stable, collision-free cache key that matches what
        /// <see cref="System.Type.FullName"/> produces, so registration (which has the
        /// assembly-qualified name) and lookup (which has <c>FullName</c>) agree.
        /// Bracket-aware: commas inside generic argument lists (<c>[[...]]</c>) are ignored.
        /// </summary>
        internal static string ToFullClrName(string clrTypeName)
        {
            int depth = 0;
            for (int i = 0; i < clrTypeName.Length; i++)
            {
                char c = clrTypeName[i];
                if (c == '[')
                {
                    depth++;
                }
                else if (c == ']')
                {
                    depth--;
                }
                else if (c == ',' && depth == 0)
                {
                    return clrTypeName.Substring(0, i);
                }
            }

            return clrTypeName;
        }

        private static string StripAllArity(string name)
        {
            int backtick = name.IndexOf('`', StringComparison.Ordinal);
            if (backtick < 0)
            {
                return name;
            }

            var builder = new System.Text.StringBuilder(name.Length);
            int i = 0;
            while (i < name.Length)
            {
                char c = name[i];
                if (c == '`')
                {
                    i++;
                    while (i < name.Length && char.IsDigit(name[i]))
                    {
                        i++;
                    }
                }
                else
                {
                    builder.Append(c);
                    i++;
                }
            }

            return builder.ToString();
        }
    }
}
