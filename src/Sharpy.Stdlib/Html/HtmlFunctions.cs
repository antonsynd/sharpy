using System.Text;

namespace Sharpy
{
    public static partial class Html
    {
        /// <summary>
        /// Replace special characters "&amp;", "&lt;", "&gt;" with HTML-safe sequences.
        /// When <paramref name="quote"/> is true (default), characters '"' and '\'' are also translated.
        /// </summary>
        /// <param name="s">The string to escape.</param>
        /// <param name="quote">Whether to escape quote characters.</param>
        /// <returns>The escaped string.</returns>
        public static string Escape(string s, bool quote = true)
        {
            if (s == null)
            {
                throw new TypeError("expected str, got NoneType");
            }

            // Order matters: & must be replaced first to avoid double-escaping
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '&':
                        sb.Append("&amp;");
                        break;
                    case '<':
                        sb.Append("&lt;");
                        break;
                    case '>':
                        sb.Append("&gt;");
                        break;
                    case '"' when quote:
                        sb.Append("&quot;");
                        break;
                    case '\'' when quote:
                        sb.Append("&#x27;");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Convert all named and numeric character references (e.g. &amp;gt;, &amp;#62;,
        /// &amp;#x3e;) in the string to the corresponding Unicode characters.
        /// </summary>
        /// <param name="s">The string to unescape.</param>
        /// <returns>The unescaped string.</returns>
        public static string Unescape(string s)
        {
            if (s == null)
            {
                throw new TypeError("expected str, got NoneType");
            }

            return System.Net.WebUtility.HtmlDecode(s);
        }
    }
}
