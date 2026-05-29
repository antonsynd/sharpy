using System;
using System.Net;

namespace Sharpy
{
    /// <summary>
    /// Python-compatible html module.
    /// Provides functions for manipulating HTML, including escaping and unescaping HTML entities.
    /// </summary>
    public static partial class HtmlModule
    {
        /// <summary>
        /// Convert the characters &amp;, &lt;, &gt;, ", and ' in string s to HTML-safe sequences.
        /// </summary>
        /// <param name="s">The string to escape.</param>
        /// <param name="quote">
        /// If true (default), the characters " and ' are also translated.
        /// </param>
        /// <returns>The escaped string.</returns>
        /// <example>
        /// <code>
        /// html.escape("&lt;script&gt;alert('xss')&lt;/script&gt;")
        /// // "&amp;lt;script&amp;gt;alert(&amp;#x27;xss&amp;#x27;)&amp;lt;/script&amp;gt;"
        /// </code>
        /// </example>
        public static string Escape(string s, bool quote = true)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s ?? string.Empty;
            }

            // Python's html.escape replaces &, <, >, and optionally " and '
            // We implement this manually to match Python's exact behavior
            s = s.Replace("&", "&amp;");
            s = s.Replace("<", "&lt;");
            s = s.Replace(">", "&gt;");

            if (quote)
            {
                s = s.Replace("\"", "&quot;");
                s = s.Replace("'", "&#x27;");
            }

            return s;
        }

        /// <summary>
        /// Convert all named and numeric character references (e.g., &amp;gt;, &amp;#62;, &amp;#x3e;)
        /// in the string s to the corresponding Unicode characters.
        /// </summary>
        /// <param name="s">The string to unescape.</param>
        /// <returns>The unescaped string.</returns>
        /// <example>
        /// <code>
        /// html.unescape("&amp;lt;b&amp;gt;bold&amp;lt;/b&amp;gt;")
        /// // "&lt;b&gt;bold&lt;/b&gt;"
        /// </code>
        /// </example>
        public static string Unescape(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s ?? string.Empty;
            }

            return WebUtility.HtmlDecode(s);
        }
    }
}
