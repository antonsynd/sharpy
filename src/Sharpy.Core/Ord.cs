namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Return the Unicode code point for a one-character string.
        /// This is the inverse of chr().
        /// </summary>
        /// <param name="s">A one-character string</param>
        /// <returns>The Unicode code point of the character</returns>
        /// <exception cref="TypeError">Thrown when the string is not exactly one character</exception>
        /// <example>
        /// <code>
        /// ord("A")    # 65
        /// ord("€")    # 8364
        /// ord("a")    # 97
        /// </code>
        /// </example>
        public static int Ord(string s)
        {
            if (s == null || s.Length == 0)
            {
                throw new TypeError("ord() expected a character, but string of length 0 found");
            }

            // Handle surrogate pairs for full Unicode support
            if (char.IsHighSurrogate(s[0]) && s.Length >= 2 && char.IsLowSurrogate(s[1]))
            {
                if (s.Length > 2)
                {
                    throw new TypeError($"ord() expected a character, but string of length {s.Length} found");
                }

                return char.ConvertToUtf32(s[0], s[1]);
            }

            if (s.Length != 1)
            {
                throw new TypeError($"ord() expected a character, but string of length {s.Length} found");
            }

            return s[0];
        }
    }
}
