using System.Text;

namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Return a string with non-ASCII characters escaped.
        /// Calls repr() first, then escapes non-ASCII characters with \xNN, \uNNNN, or \UNNNNNNNN.
        /// </summary>
        /// <example>
        /// <code>
        /// ascii("hello")      # "'hello'"
        /// ascii("héllo")      # "'h\\xe9llo'"
        /// </code>
        /// </example>
        public static string Ascii(object obj)
        {
            string s = Repr(obj);

            bool hasNonAscii = false;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] > 127)
                {
                    hasNonAscii = true;
                    break;
                }
            }

            if (!hasNonAscii)
            {
                return s;
            }

            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c <= 127)
                {
                    sb.Append(c);
                }
                else if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                {
                    int codePoint = char.ConvertToUtf32(c, s[i + 1]);
                    sb.Append("\\U" + codePoint.ToString("x8"));
                    i++;
                }
                else if (c <= 0xFF)
                {
                    sb.Append("\\x" + ((int)c).ToString("x2"));
                }
                else
                {
                    sb.Append("\\u" + ((int)c).ToString("x4"));
                }
            }

            return sb.ToString();
        }
    }
}
