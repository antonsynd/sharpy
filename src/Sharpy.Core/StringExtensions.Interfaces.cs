using System;

namespace Sharpy
{
    /// <summary>
    /// Containment extension methods for string.
    /// </summary>
    public static partial class StringExtensions
    {
        /// <summary>
        /// Return true if <paramref name="substring"/> is found within this string.
        /// Used for <c>"x" in s</c> codegen.
        /// </summary>
        /// <remarks>
        /// This extension shadows <see cref="string.Contains(string)"/> by design.
        /// C# instance methods take precedence over extensions, so generated code calling
        /// <c>s.Contains(x)</c> always invokes the BCL method at runtime. This overload
        /// exists so that BuiltinRegistry can discover it via reflection and register
        /// <c>str.contains</c> for type-checking. The ordinal semantics here match
        /// Python's byte-level containment check.
        /// </remarks>
        public static bool Contains(this string s, string substring)
        {
            return s.IndexOf(substring, StringComparison.Ordinal) >= 0;
        }
    }
}
