using System;
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
        private const string InlineFlagChars = "aiLmsux";

        public static string Translate(string pattern)
        {
            if (pattern == null)
            {
                throw new TypeError("expected string, got NoneType");
            }

            // Fast path: no Python-specific syntax
            if (!NeedsTranslation(pattern))
            {
                return pattern;
            }

            var sb = new StringBuilder(pattern.Length);
            int i = 0;

            while (i < pattern.Length)
            {
                // Handle escape sequences — pass through without translation
                if (pattern[i] == '\\' && i + 1 < pattern.Length)
                {
                    sb.Append(pattern[i]);
                    sb.Append(pattern[i + 1]);
                    i += 2;
                    continue;
                }

                // Check for (? sequences
                if (pattern[i] == '(' &&
                    i + 1 < pattern.Length &&
                    pattern[i + 1] == '?')
                {
                    // (?P<name>...) or (?P=name)
                    if (i + 2 < pattern.Length && pattern[i + 2] == 'P')
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

                    // Inline flags: (?aiLmsux) or (?aiLmsux:...)
                    if (i + 2 < pattern.Length && IsInlineFlagChar(pattern[i + 2]))
                    {
                        int flagStart = i + 2;
                        int j = flagStart;
                        while (j < pattern.Length && IsInlineFlagChar(pattern[j]))
                        {
                            j++;
                        }

                        if (j < pattern.Length && (pattern[j] == ')' || pattern[j] == ':'))
                        {
                            // Collect flag chars, strip a/u/L, keep i/m/s/x
                            string flags = pattern.Substring(flagStart, j - flagStart);
                            string filtered = FilterInlineFlags(flags);

                            if (pattern[j] == ')')
                            {
                                // (?flags) form
                                if (filtered.Length > 0)
                                {
                                    sb.Append("(?");
                                    sb.Append(filtered);
                                    sb.Append(')');
                                }

                                // If all flags were stripped, skip the entire group
                                i = j + 1;
                                continue;
                            }
                            else
                            {
                                // (?flags:...) form
                                if (filtered.Length > 0)
                                {
                                    sb.Append("(?");
                                    sb.Append(filtered);
                                    sb.Append(':');
                                }
                                else
                                {
                                    // All flags stripped, emit as non-capturing group
                                    sb.Append("(?:");
                                }

                                i = j + 1; // skip past the ':'
                                continue;
                            }
                        }
                    }
                }

                sb.Append(pattern[i]);
                i++;
            }

            return sb.ToString();
        }

        private static bool NeedsTranslation(string pattern)
        {
            for (int i = 0; i < pattern.Length; i++)
            {
                if (pattern[i] == '\\' && i + 1 < pattern.Length)
                {
                    i++; // skip escaped char
                    continue;
                }

                if (pattern[i] == '(' && i + 1 < pattern.Length && pattern[i + 1] == '?')
                {
                    // (?P... needs translation
                    if (i + 2 < pattern.Length && pattern[i + 2] == 'P')
                        return true;
                    // (?<inline flags with a/u/L> needs translation
                    if (i + 2 < pattern.Length && IsInlineFlagChar(pattern[i + 2]))
                    {
                        int j = i + 2;
                        while (j < pattern.Length && IsInlineFlagChar(pattern[j]))
                        {
                            j++;
                        }

                        if (j < pattern.Length && (pattern[j] == ')' || pattern[j] == ':'))
                        {
                            string flags = pattern.Substring(i + 2, j - (i + 2));
                            if (flags.IndexOf('a') >= 0 || flags.IndexOf('u') >= 0 || flags.IndexOf('L') >= 0)
                                return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool IsInlineFlagChar(char c)
        {
            return InlineFlagChars.IndexOf(c) >= 0;
        }

        private static string FilterInlineFlags(string flags)
        {
            var sb = new StringBuilder(flags.Length);
            foreach (char c in flags)
            {
                // Strip Python-only flags: a (ASCII), u (UNICODE), L (LOCALE)
                if (c != 'a' && c != 'u' && c != 'L')
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }
    }
}
