using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Sharpy
{
    /// <summary>
    /// Unix filename pattern matching, matching Python's fnmatch module.
    /// </summary>
    public static partial class Fnmatch
    {
        /// <summary>
        /// Test whether <paramref name="name"/> matches <paramref name="pat"/>.
        /// The pattern uses Unix shell-style wildcards:
        /// <c>*</c> matches everything, <c>?</c> matches any single character,
        /// <c>[seq]</c> matches any character in seq, <c>[!seq]</c> matches any
        /// character not in seq.
        /// On Windows, the comparison is case-insensitive. On Unix, it is
        /// case-sensitive.
        /// </summary>
        /// <param name="name">The filename to test.</param>
        /// <param name="pat">The pattern to match against.</param>
        /// <returns><c>true</c> if <paramref name="name"/> matches <paramref name="pat"/>.</returns>
        public static bool FnMatch(string name, string pat)
        {
            if (name == null)
            {
                throw new TypeError("argument must be str, not NoneType");
            }

            if (pat == null)
            {
                throw new TypeError("argument must be str, not NoneType");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                name = name.ToLowerInvariant();
                pat = pat.ToLowerInvariant();
            }

            return FnMatchCase(name, pat);
        }

        /// <summary>
        /// Test whether <paramref name="name"/> matches <paramref name="pat"/>,
        /// using a case-sensitive comparison regardless of the platform.
        /// </summary>
        /// <param name="name">The filename to test.</param>
        /// <param name="pat">The pattern to match against.</param>
        /// <returns><c>true</c> if <paramref name="name"/> matches <paramref name="pat"/>.</returns>
        public static bool FnMatchCase(string name, string pat)
        {
            if (name == null)
            {
                throw new TypeError("argument must be str, not NoneType");
            }

            if (pat == null)
            {
                throw new TypeError("argument must be str, not NoneType");
            }

            string regexPattern = Translate(pat);
            return Regex.IsMatch(name, regexPattern);
        }

        /// <summary>
        /// Return the subset of the list of <paramref name="names"/> that match
        /// <paramref name="pat"/>. Same as
        /// <c>[n for n in names if fnmatch(n, pat)]</c> but more efficient.
        /// </summary>
        /// <param name="names">The list of filenames to filter.</param>
        /// <param name="pat">The pattern to match against.</param>
        /// <returns>A new list of matching filenames.</returns>
        public static Sharpy.List<string> Filter(Sharpy.List<string> names, string pat)
        {
            if (names == null)
            {
                throw new TypeError("argument must be list, not NoneType");
            }

            if (pat == null)
            {
                throw new TypeError("argument must be str, not NoneType");
            }

            string matchPat = pat;
            bool caseInsensitive = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            if (caseInsensitive)
            {
                matchPat = matchPat.ToLowerInvariant();
            }

            string regexPattern = Translate(matchPat);
            var regex = new Regex(regexPattern);
            var result = new System.Collections.Generic.List<string>();

            foreach (string name in names)
            {
                string testName = caseInsensitive ? name.ToLowerInvariant() : name;
                if (regex.IsMatch(testName))
                {
                    result.Add(name);
                }
            }

            return new Sharpy.List<string>(result);
        }

        /// <summary>
        /// Translate a shell-style <paramref name="pat"/> to a regular expression.
        /// The resulting string will be a regex pattern suitable for use with
        /// <see cref="Regex.IsMatch(string, string)"/>.
        /// </summary>
        /// <param name="pat">The fnmatch pattern to translate.</param>
        /// <returns>A regex string equivalent to the pattern.</returns>
        public static string Translate(string pat)
        {
            if (pat == null)
            {
                throw new TypeError("argument must be str, not NoneType");
            }

            var sb = new StringBuilder();
            sb.Append("\\A(?s:");
            int i = 0;

            while (i < pat.Length)
            {
                char c = pat[i];
                i++;

                if (c == '*')
                {
                    sb.Append(".*");
                }
                else if (c == '?')
                {
                    sb.Append('.');
                }
                else if (c == '[')
                {
                    int j = i;

                    // Find closing bracket
                    if (j < pat.Length && pat[j] == '!')
                    {
                        j++;
                    }

                    if (j < pat.Length && pat[j] == ']')
                    {
                        j++;
                    }

                    while (j < pat.Length && pat[j] != ']')
                    {
                        j++;
                    }

                    if (j >= pat.Length)
                    {
                        // No closing bracket, treat '[' as literal
                        sb.Append("\\[");
                    }
                    else
                    {
                        string stuff = pat.Substring(i, j - i);
                        i = j + 1;

                        // Replace backslashes for regex
                        stuff = stuff.Replace("\\", "\\\\");

                        // Translate negation
                        if (stuff.Length > 0 && stuff[0] == '!')
                        {
                            stuff = "^" + stuff.Substring(1);
                        }

                        sb.Append('[');
                        sb.Append(stuff);
                        sb.Append(']');
                    }
                }
                else
                {
                    sb.Append(Regex.Escape(c.ToString()));
                }
            }

            sb.Append(")\\Z");
            return sb.ToString();
        }
    }
}
