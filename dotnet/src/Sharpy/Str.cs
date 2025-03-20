using System.Runtime.CompilerServices;
using Sharpy.Collections.Interfaces;

namespace Sharpy
{
    public static partial class Builtins
    {
        /// <remarks>
        /// <see cref="Object.ToString"/> calls <see cref="Object.__Str__"/>
        /// so this implementation covers all native C# objects and Sharpy
        /// objects.
        /// </remarks>
        public static string Str(object x)
        {
            return x.ToString() ?? "";
        }

        // public static string Str(bytes, encoding, errors)

        public static uint __Len__(this string s)
        {
            return (uint)s.Length;
        }

        public static string Capitalize(this string s)
        {
            return s;
        }

        public static string CaseFold(this string s)
        {
            return s;
        }
        public static string Center(this string s, uint width, string fillchar = " ")
        {
            return s;
        }
        public static string Count(this string s, string sub, uint start, uint end)
        {
            return s;
        }
        public static string Encode(this string s, string encoding = "utf-8", string errors = "strict")
        {
            return s;
        }
        public static string EndsWith(this string s, string encoding = "utf-8", string errors = "strict")
        {
            return s;
        }
        public static string ExpandTabs(this string s, string encoding = "utf-8", string errors = "strict")
        {
            return s;
        }
        public static string Find(this string s, string encoding = "utf-8", string errors = "strict")
        {
            return s;
        }
        public static string Format(this string s, string encoding = "utf-8", string errors = "strict")
        {
            return s;
        }
        public static string FormatMap(this string s, string encoding = "utf-8", string errors = "strict")
        {
            return s;
        }
        public static string Index(this string s, string encoding = "utf-8", string errors = "strict")
        {
            return s;
        }
        public static bool IsAlnum(this string s)
        {
            return false;
        }
        public static bool IsAlpha(this string s)
        {
            return false;
        }
        public static bool IsAscii(this string s)
        {
            return false;
        }
        public static bool IsDecimal(this string s)
        {
            return false;
        }

        public static bool IsIdentifier(this string s)
        {
            return false;
        }

        public static bool IsLower(this string s)
        {
            return false;
        }

        public static bool IsNumeric(this string s)
        {
            return false;
        }

        public static bool IsPrintable(this string s)
        {
            return false;
        }

        public static bool IsSpace(this string s)
        {
            return false;
        }

        public static bool IsTitle(this string s)
        {
            return false;
        }

        public static bool IsUpper(this string s)
        {
            return false;
        }

        public static string Join(this string s, Iterable<string> iterable)
        {
            return s;
        }

        public static void LJust(this string s, uint width, string fillchar)
        {
        }

        public static string Lower(this string s)
        {
            return s;
        }

        public static void LStrip(this string s, string chars)
        {
        }

        public static void MakeTrans(Mapping<string, string?> mapping)
        {
        }

        public static void MakeTrans(Mapping<uint, string?> mapping)
        {
        }

        public static void MakeTrans(string fromChars, string toChars)
        {
        }

        public static void MakeTrans(string fromChars, string toChars, string ignoreChars)
        {
        }

        public static void Partition(this string s, string sep)
        {
        }

        public static void RemovePrefix(this string s, string prefix)
        {
        }

        public static void RemoveSuffix(this string s, string suffix)
        {
        }

        public static void Replace(this string s, string old, string @new, uint count) {
        }

        public static void RFind(this string s, string sub, int start = 0, int end = -1)
        {
        }

        public static void RIndex(this string s, string sub, int start = 0, int end = -1)
        {
        }

        public static void RJust(this string s, uint width, string fillchar)
        {
        }

        public static void RPartition(this string s, string sep)
        {
        }

        public static void RSplit(this string s, string sep, uint maxsplit)
        {
        }

        public static void RStrip(this string s, string chars)
        {
        }

        public static void Split(this string s, string sep, uint maxsplit)
        {
        }

        public static void SplitLines(this string s, bool keepends = false)
        {
        }

        public static void StartsWith(this string s, string prefix, int start = 0, int end = -1)
        {
        }

        public static void Strip(this string s, string chars)
        {
        }

        public static void SwapCase(this string s)
        {
        }

        public static void Title(this string s)
        {
        }

        public static void Translate(this string s, uint table)
        {
        }

        public static void Upper(this string s)
        {
        }

        public static void ZFill(this string s, uint width)
        {

        }
    }
}
