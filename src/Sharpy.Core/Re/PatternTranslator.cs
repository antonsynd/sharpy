using System.Text;
using System.Text.RegularExpressions;

namespace Sharpy
{
    /// <summary>
    /// Translates Python regex syntax differences to .NET regex syntax.
    /// </summary>
    internal static class RePatternTranslator
    {
        /// <summary>
        /// Translate Python-specific regex syntax to .NET-compatible syntax.
        /// Handles:
        ///   (?P&lt;name&gt;...) → (?&lt;name&gt;...)
        ///   (?P=name) → \k&lt;name&gt;
        /// </summary>
        public static string Translate(string pattern)
        {
            if (pattern == null)
            {
                throw new TypeError("expected string, got NoneType");
            }

            // Fast path: no Python-specific syntax
            if (pattern.IndexOf("(?P") < 0)
            {
                return pattern;
            }

            var sb = new StringBuilder(pattern.Length);
            int i = 0;

            while (i < pattern.Length)
            {
                // Check for (?P<name>...) or (?P=name)
                if (i + 3 < pattern.Length &&
                    pattern[i] == '(' &&
                    pattern[i + 1] == '?' &&
                    pattern[i + 2] == 'P')
                {
                    if (i + 3 < pattern.Length && pattern[i + 3] == '<')
                    {
                        // (?P<name>...) → (?<name>...)
                        sb.Append("(?<");
                        i += 4; // skip (?P<
                        continue;
                    }

                    if (i + 3 < pattern.Length && pattern[i + 3] == '=')
                    {
                        // (?P=name) → \k<name>
                        i += 4; // skip (?P=
                        int nameStart = i;
                        while (i < pattern.Length && pattern[i] != ')')
                        {
                            i++;
                        }

                        string name = pattern.Substring(nameStart, i - nameStart);
                        sb.Append("\\k<");
                        sb.Append(name);
                        sb.Append('>');

                        if (i < pattern.Length)
                        {
                            i++; // skip closing )
                        }

                        continue;
                    }
                }

                // Handle escape sequences — pass through without translation
                if (pattern[i] == '\\' && i + 1 < pattern.Length)
                {
                    sb.Append(pattern[i]);
                    sb.Append(pattern[i + 1]);
                    i += 2;
                    continue;
                }

                sb.Append(pattern[i]);
                i++;
            }

            return sb.ToString();
        }
    }
}
