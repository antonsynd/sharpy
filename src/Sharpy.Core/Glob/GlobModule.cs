using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Sharpy
{
    /// <summary>
    /// Unix-style pathname pattern expansion, similar to Python's glob module.
    /// Supports <c>*</c>, <c>?</c>, <c>[seq]</c>, and <c>**</c> patterns.
    /// </summary>
    public static partial class GlobModule
    {
        /// <summary>
        /// Return a sorted list of pathnames matching a pathname pattern.
        /// Similar to Python's <c>glob.glob()</c>.
        /// </summary>
        /// <param name="pattern">A glob pattern (e.g., "*.txt", "**/*.cs").</param>
        /// <returns>A sorted list of matching paths. Empty list if no matches.</returns>
        /// <example>
        /// <code>
        /// glob.glob("*.txt")          # ["a.txt", "b.txt"]
        /// glob.glob("**/*.cs")        # recursive search for .cs files
        /// glob.glob("src/[ab]*.py")   # files starting with a or b
        /// </code>
        /// </example>
        public static List<string> Glob(string pattern)
        {
            var results = new System.Collections.Generic.List<string>(Iglob(pattern));
            results.Sort(StringComparer.Ordinal);
            return new List<string>(results);
        }

        /// <summary>
        /// Return an iterator which yields the same values as <see cref="Glob"/>
        /// without actually storing them all simultaneously.
        /// Similar to Python's <c>glob.iglob()</c>.
        /// </summary>
        /// <param name="pattern">A glob pattern (e.g., "*.txt", "**/*.cs").</param>
        /// <returns>An enumerable of matching paths.</returns>
        public static IEnumerable<string> Iglob(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                yield break;
            }

            // Normalize separators
            string normalizedPattern = pattern.Replace('\\', '/');

            // Split into directory base and pattern parts
            string baseDir;
            string filePattern;
            bool recursive = normalizedPattern.Contains("**");

            SplitPattern(normalizedPattern, out baseDir, out filePattern);

            if (string.IsNullOrEmpty(baseDir))
            {
                baseDir = ".";
            }

            if (!Directory.Exists(baseDir))
            {
                yield break;
            }

            // Build regex from the file pattern
            Regex regex = PatternToRegex(filePattern);
            SearchOption searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            IEnumerable<string> entries;
            try
            {
                var files = Directory.EnumerateFiles(baseDir, "*", searchOption);
                var dirs = Directory.EnumerateDirectories(baseDir, "*", searchOption);
                entries = files.Concat(dirs);
            }
            catch (Exception)
            {
                yield break;
            }

            foreach (string entry in entries)
            {
                // Get relative path from base directory for matching
                string relativePath = GetRelativePath(baseDir, entry);
                // Normalize to forward slashes for matching
                string normalizedRelative = relativePath.Replace('\\', '/');

                if (regex.IsMatch(normalizedRelative))
                {
                    yield return entry;
                }
            }
        }

        /// <summary>
        /// Escape all special characters in a pathname.
        /// Similar to Python's <c>glob.escape()</c>.
        /// Special characters <c>*</c>, <c>?</c>, and <c>[</c> are escaped
        /// by wrapping them in brackets.
        /// </summary>
        /// <param name="pathname">The pathname to escape.</param>
        /// <returns>The escaped pathname.</returns>
        /// <example>
        /// <code>
        /// glob.escape("file[1].txt")    # "file[[]1].txt"
        /// glob.escape("*.py")           # "[*].py"
        /// </code>
        /// </example>
        public static string Escape(string pathname)
        {
            if (string.IsNullOrEmpty(pathname))
            {
                return pathname;
            }

            var sb = new StringBuilder(pathname.Length);
            foreach (char c in pathname)
            {
                switch (c)
                {
                    case '*':
                        sb.Append("[*]");
                        break;
                    case '?':
                        sb.Append("[?]");
                        break;
                    case '[':
                        sb.Append("[[]");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private static void SplitPattern(string pattern, out string baseDir, out string filePattern)
        {
            // Find the first segment that contains a wildcard
            string[] segments = pattern.Split('/');
            int firstWildcard = -1;

            for (int i = 0; i < segments.Length; i++)
            {
                if (HasMagic(segments[i]))
                {
                    firstWildcard = i;
                    break;
                }
            }

            if (firstWildcard < 0)
            {
                // No wildcards — treat entire pattern as a literal path
                baseDir = System.IO.Path.GetDirectoryName(pattern.Replace('/', System.IO.Path.DirectorySeparatorChar)) ?? ".";
                filePattern = System.IO.Path.GetFileName(pattern.Replace('/', System.IO.Path.DirectorySeparatorChar));
                return;
            }

            if (firstWildcard == 0)
            {
                baseDir = ".";
                filePattern = pattern;
            }
            else
            {
                // Join segments before the wildcard as the base directory
                var baseParts = new string[firstWildcard];
                Array.Copy(segments, baseParts, firstWildcard);
                baseDir = string.Join(System.IO.Path.DirectorySeparatorChar.ToString(), baseParts);

                // Remaining segments form the file pattern
                var patternParts = new string[segments.Length - firstWildcard];
                Array.Copy(segments, firstWildcard, patternParts, 0, patternParts.Length);
                filePattern = string.Join("/", patternParts);
            }
        }

        private static bool HasMagic(string s)
        {
            return s.Contains('*') || s.Contains('?') || s.Contains('[');
        }

        private static Regex PatternToRegex(string pattern)
        {
            var sb = new StringBuilder();
            sb.Append('^');

            int i = 0;
            while (i < pattern.Length)
            {
                char c = pattern[i];
                switch (c)
                {
                    case '*':
                        if (i + 1 < pattern.Length && pattern[i + 1] == '*')
                        {
                            // ** matches everything including path separators
                            sb.Append(".*");
                            i += 2;
                            // Skip trailing separator after **
                            if (i < pattern.Length && pattern[i] == '/')
                            {
                                i++;
                            }
                        }
                        else
                        {
                            // * matches everything except path separators
                            sb.Append("[^/]*");
                            i++;
                        }
                        break;
                    case '?':
                        sb.Append("[^/]");
                        i++;
                        break;
                    case '[':
                        // Character class — find matching ]
                        int end = pattern.IndexOf(']', i + 1);
                        if (end < 0)
                        {
                            sb.Append(Regex.Escape(c.ToString()));
                            i++;
                        }
                        else
                        {
                            string charClass = pattern.Substring(i, end - i + 1);
                            sb.Append(charClass);
                            i = end + 1;
                        }
                        break;
                    case '/':
                        sb.Append("[/\\\\]");
                        i++;
                        break;
                    default:
                        sb.Append(Regex.Escape(c.ToString()));
                        i++;
                        break;
                }
            }

            sb.Append('$');
            return new Regex(sb.ToString(), RegexOptions.Compiled);
        }

        private static string GetRelativePath(string basePath, string fullPath)
        {
            string fullBase = System.IO.Path.GetFullPath(basePath);
            string fullTarget = System.IO.Path.GetFullPath(fullPath);

            if (!fullBase.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()))
            {
                fullBase += System.IO.Path.DirectorySeparatorChar;
            }

            if (fullTarget.StartsWith(fullBase, StringComparison.Ordinal))
            {
                return fullTarget.Substring(fullBase.Length);
            }

            // Fallback: return the full path
            return fullTarget;
        }
    }
}
