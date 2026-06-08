using System;
using System.Text.RegularExpressions;

namespace Sharpy
{
    /// <summary>
    /// Regular expression operations.
    /// <para>
    /// The classes (Pattern, Match, Error), pattern translator, and flag constants
    /// are generated from <c>src/Sharpy.Stdlib/spy/re_module.spy</c> into <c>ReModule.cs</c>.
    /// Module-level convenience functions and callable sub/subn overloads stay here
    /// because they involve delegate interop and name-resolution constraints.
    /// </para>
    /// </summary>
    [SharpyModule("re")]
    public static partial class ReModule
    {
        // ---- Module-level functions ----

        /// <summary>Compile a pattern into a Pattern object.</summary>
        public static Pattern Compile(string pattern, int flags = 0)
        {
            return new Pattern(pattern, flags);
        }

        /// <summary>Scan through string looking for the first match.</summary>
        public static MatchResult? Search(string pattern, string s, int flags = 0)
        {
            return Compile(pattern, flags).Search(s);
        }

        /// <summary>Try to apply the pattern at the start of the string.</summary>
        public static MatchResult? Match(string pattern, string s, int flags = 0)
        {
            return Compile(pattern, flags).Match(s);
        }

        /// <summary>Try to apply the pattern to the entire string.</summary>
        public static MatchResult? Fullmatch(string pattern, string s, int flags = 0)
        {
            return Compile(pattern, flags).Fullmatch(s);
        }

        /// <summary>Return all non-overlapping matches of pattern in string.</summary>
        public static List<object> Findall(string pattern, string s, int flags = 0)
        {
            return Compile(pattern, flags).Findall(s);
        }

        /// <summary>Return a list of match objects over all non-overlapping matches.</summary>
        public static List<MatchResult> Finditer(string pattern, string s, int flags = 0)
        {
            return Compile(pattern, flags).Finditer(s);
        }

        /// <summary>Return the string obtained by replacing occurrences.</summary>
        public static string Sub(string pattern, string repl, string s, int count = 0, int flags = 0)
        {
            return Compile(pattern, flags).Sub(repl, s, count);
        }

        /// <summary>Return the string obtained by replacing occurrences using a callable.</summary>
        public static string Sub(string pattern, Func<MatchResult, string> repl, string s, int count = 0, int flags = 0)
        {
            return Compile(pattern, flags).Sub(repl, s, count);
        }

        /// <summary>Like Sub(), but returns (new_string, number_of_subs_made).</summary>
        public static (string, int) Subn(string pattern, string repl, string s, int count = 0, int flags = 0)
        {
            return Compile(pattern, flags).Subn(repl, s, count);
        }

        /// <summary>Like Sub() with callable, but returns (new_string, number_of_subs_made).</summary>
        public static (string, int) Subn(string pattern, Func<MatchResult, string> repl, string s, int count = 0, int flags = 0)
        {
            return Compile(pattern, flags).Subn(repl, s, count);
        }

        /// <summary>Split string by the occurrences of pattern.</summary>
        public static List<string> Split(string pattern, string s, int maxsplit = 0, int flags = 0)
        {
            return Compile(pattern, flags).Split(s, maxsplit);
        }

        /// <summary>Clear the regular expression cache (no-op on .NET).</summary>
        public static void Purge()
        {
        }

        /// <summary>Escape special characters in pattern.</summary>
        public static string Escape(string pattern)
        {
            return Regex.Escape(pattern);
        }
    }

    /// <summary>
    /// Extension methods for Pattern's callable sub/subn overloads.
    /// These use MatchEvaluator delegates that are cleaner in C#.
    /// </summary>
    public static class RePatternExtensions
    {
        /// <summary>
        /// Return the string obtained by replacing occurrences using a callable.
        /// </summary>
        public static string Sub(this ReModule.Pattern pattern, Func<ReModule.MatchResult, string> repl, string s, int count = 0)
        {
            int replaced = 0;
            return pattern.Regex.Replace(s, m =>
            {
                if (count > 0 && replaced >= count)
                    return m.Value;
                replaced++;
                var reMatch = new ReModule.MatchResult(m, s, pattern.PatternStr, 0, s.Length, pattern);
                return repl(reMatch);
            });
        }

        /// <summary>
        /// Like Sub(), but returns (new_string, number_of_subs_made).
        /// </summary>
        public static (string, int) Subn(this ReModule.Pattern pattern, string repl, string s, int count = 0)
        {
            string translated = ReModule._TranslateReplacement(repl);
            int replacementCount = 0;
            string result;
            if (count == 0)
            {
                result = pattern.Regex.Replace(s, m =>
                {
                    replacementCount++;
                    return m.Result(translated);
                });
            }
            else
            {
                result = pattern.Regex.Replace(s, m =>
                {
                    replacementCount++;
                    return m.Result(translated);
                }, count);
            }

            return (result, replacementCount);
        }

        /// <summary>
        /// Like Sub() with callable, but returns (new_string, number_of_subs_made).
        /// </summary>
        public static (string, int) Subn(this ReModule.Pattern pattern, Func<ReModule.MatchResult, string> repl, string s, int count = 0)
        {
            int replacementCount = 0;
            string result = pattern.Regex.Replace(s, m =>
            {
                if (count > 0 && replacementCount >= count)
                    return m.Value;
                replacementCount++;
                var reMatch = new ReModule.MatchResult(m, s, pattern.PatternStr, 0, s.Length, pattern);
                return repl(reMatch);
            });
            return (result, replacementCount);
        }
    }
}
