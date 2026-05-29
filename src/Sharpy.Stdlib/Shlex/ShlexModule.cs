using System;
using System.Collections.Generic;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Shell-like lexical analysis — split, quote, and join strings like POSIX shell.
    /// Matches the Python <c>shlex</c> module API.
    /// </summary>
    public static partial class ShlexModule
    {
        /// <summary>
        /// Split the string <paramref name="s"/> using shell-like syntax (POSIX mode).
        /// </summary>
        /// <param name="s">The string to split.</param>
        /// <returns>A list of tokens.</returns>
        /// <exception cref="ValueError">Thrown when quotes are not closed.</exception>
        public static List<string> Split(string s)
        {
            if (s == null)
            {
                throw new TypeError("argument must be str, not NoneType");
            }

            var tokens = new List<string>();
            var token = new StringBuilder();
            bool inToken = false;
            int i = 0;

            while (i < s.Length)
            {
                char c = s[i];

                if (c == '\\')
                {
                    // Backslash escaping outside quotes
                    inToken = true;
                    i++;
                    if (i < s.Length)
                    {
                        token.Append(s[i]);
                        i++;
                    }
                    else
                    {
                        // Trailing backslash — keep it literal
                        token.Append('\\');
                    }
                }
                else if (c == '\'')
                {
                    // Single-quoted string: no escaping inside
                    inToken = true;
                    i++;
                    while (i < s.Length && s[i] != '\'')
                    {
                        token.Append(s[i]);
                        i++;
                    }

                    if (i >= s.Length)
                    {
                        throw new ValueError("No closing quotation");
                    }

                    i++; // skip closing quote
                }
                else if (c == '"')
                {
                    // Double-quoted string: backslash escaping for \, ", $, `, newline
                    inToken = true;
                    i++;
                    while (i < s.Length && s[i] != '"')
                    {
                        if (s[i] == '\\')
                        {
                            i++;
                            if (i < s.Length)
                            {
                                char next = s[i];
                                // In POSIX mode, only these chars are special inside double quotes
                                if (next == '\\' || next == '"' || next == '$' || next == '`' || next == '\n')
                                {
                                    token.Append(next);
                                }
                                else
                                {
                                    token.Append('\\');
                                    token.Append(next);
                                }

                                i++;
                            }
                            else
                            {
                                token.Append('\\');
                            }
                        }
                        else
                        {
                            token.Append(s[i]);
                            i++;
                        }
                    }

                    if (i >= s.Length)
                    {
                        throw new ValueError("No closing quotation");
                    }

                    i++; // skip closing quote
                }
                else if (c == ' ' || c == '\t' || c == '\n' || c == '\r')
                {
                    // Whitespace — finalize current token
                    if (inToken)
                    {
                        tokens.Add(token.ToString());
                        token.Clear();
                        inToken = false;
                    }

                    i++;
                }
                else
                {
                    // Regular character
                    inToken = true;
                    token.Append(c);
                    i++;
                }
            }

            if (inToken)
            {
                tokens.Add(token.ToString());
            }

            return tokens;
        }

        /// <summary>
        /// Return a shell-escaped version of the string <paramref name="s"/>.
        /// The returned value is a string that can safely be used as one token in a shell command line.
        /// </summary>
        /// <param name="s">The string to quote.</param>
        /// <returns>A safely quoted string.</returns>
        public static string Quote(string s)
        {
            if (s == null)
            {
                throw new TypeError("argument must be str, not NoneType");
            }

            if (s.Length == 0)
            {
                return "''";
            }

            // If the string is safe (only contains chars that don't need quoting), return as-is
            bool safe = true;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (!IsShellSafe(c))
                {
                    safe = false;
                    break;
                }
            }

            if (safe)
            {
                return s;
            }

            // Wrap in single quotes, escaping any existing single quotes
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('\'');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\'')
                {
                    // Close quote, add escaped single quote, reopen quote
                    sb.Append("'\"'\"'");
                }
                else
                {
                    sb.Append(c);
                }
            }

            sb.Append('\'');
            return sb.ToString();
        }

        /// <summary>
        /// Concatenate a list of tokens using <see cref="Quote"/> and join with spaces.
        /// The inverse of <see cref="Split"/>.
        /// </summary>
        /// <param name="splitCommand">The list of tokens to join.</param>
        /// <returns>A shell-safe command string.</returns>
        public static string Join(List<string> splitCommand)
        {
            if (splitCommand == null)
            {
                throw new TypeError("argument must be list, not NoneType");
            }

            var sb = new StringBuilder();
            int count = ((ICollection<string>)splitCommand).Count;
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(Quote(splitCommand[i]));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Returns true if a character is "safe" for unquoted shell usage.
        /// Matches Python's _find_unsafe regex: characters NOT in the safe set need quoting.
        /// </summary>
        private static bool IsShellSafe(char c)
        {
            // Safe characters: letters, digits, and @%+=:,./-_
            if (c >= 'a' && c <= 'z') return true;
            if (c >= 'A' && c <= 'Z') return true;
            if (c >= '0' && c <= '9') return true;
            return c == '@' || c == '%' || c == '+' || c == '=' ||
                   c == ':' || c == ',' || c == '.' || c == '/' ||
                   c == '-' || c == '_';
        }
    }
}
