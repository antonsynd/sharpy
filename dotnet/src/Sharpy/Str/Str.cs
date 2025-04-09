using Sharpy.Collections.Interfaces;

namespace Sharpy
{
    public static partial class Exports
    {
        public static Str Str(object x)
        {
            return new Str(x);
        }
    }

    /// <remarks>
    /// Unlike Python strings, Sharpy strings use C# strings as storage, so
    /// they are always UTF-16.
    /// </remarks>
    public readonly partial struct Str
    {
        private readonly string _s;

        public Str()
        {
            _s = "";
        }

        public Str(string x)
        {
            _s = x;
        }

        /// <remarks>
        /// <see cref="Object.ToString"/> calls <see cref="Object.__Str__"/>
        /// so this implementation covers all native C# objects and Sharpy
        /// objects.
        /// </remarks>
        public Str(object x)
        {
            _s = x.ToString() ?? "";
        }

        public Str(Bytes bytes, string encoding = "utf-8", string errors = "strict")
        {
            _s = "";
        }

        /// <remarks>
        /// Implicit conversion from string.
        /// </remarks>
        public static implicit operator Str(string value)
        {
            return new Str(value);
        }

        /// <remarks>
        /// Implicit conversion to string.
        /// </remarks>
        public static implicit operator string(Str value)
        {
            return value._s;
        }

        public static bool operator ==(Str lhs, string rhs)
        {
            return lhs._s == rhs;
        }

        public static bool operator !=(Str lhs, string rhs)
        {
            return !(lhs == rhs);
        }

        public static bool operator ==(string lhs, Str rhs)
        {
            return lhs == rhs._s;
        }

        public static bool operator !=(string lhs, Str rhs)
        {
            return !(lhs == rhs);
        }

        public static bool operator ==(Str lhs, Str rhs)
        {
            return lhs._s == rhs._s;
        }

        public static bool operator !=(Str lhs, Str rhs)
        {
            return !(lhs == rhs);
        }

        public override bool Equals(object? obj)
        {
            if (obj is string s)
            {
                return _s == s;
            }

            return false;
        }

        /// <remarks>
        /// Same hash code as C# strings. Both are immutable and readonly with
        /// the same logical semantics.
        /// </remarks>
        public override int GetHashCode()
        {
            return _s.GetHashCode();
        }

        public override string ToString()
        {
            return _s;
        }

        public Str Capitalize()
        {
            if (string.IsNullOrEmpty(_s))
            {
                return _s;
            }

            if (_s.Length == 1)
            {
                return _s.ToUpper();
            }

            return char.ToUpper(_s[0]) + _s[1..].ToLower();
        }

        public string CaseFold()
        {
            return _s;
        }

        public string Center(uint width, string fillchar = " ")
        {
            return _s;
        }

        public string Count(string sub, int start, int end)
        {
            return _s;
        }

        public string Encode(string encoding = "utf-8", string errors = "strict")
        {
            return _s;
        }

        public string EndsWith(string encoding = "utf-8", string errors = "strict")
        {
            return _s;
        }

        public string ExpandTabs(string encoding = "utf-8", string errors = "strict")
        {
            return _s;
        }

        public string Find(string encoding = "utf-8", string errors = "strict")
        {
            return _s;
        }

        public Str Format(string encoding = "utf-8", string errors = "strict")
        {
            return _s;
        }

        public Str FormatMap(string encoding = "utf-8", string errors = "strict")
        {
            return _s;
        }

        public Str Index(string encoding = "utf-8", string errors = "strict")
        {
            return _s;
        }

        public bool IsAlnum()
        {
            return false;
        }

        public bool IsAlpha()
        {
            return false;
        }

        public bool IsAscii()
        {
            return false;
        }

        public bool IsDecimal()
        {
            return false;
        }

        public bool IsIdentifier()
        {
            return false;
        }

        public bool IsLower()
        {
            return false;
        }

        public bool IsNumeric()
        {
            return false;
        }

        public bool IsPrintable()
        {
            return false;
        }

        public bool IsSpace()
        {
            return false;
        }

        public bool IsTitle()
        {
            return false;
        }

        public bool IsUpper()
        {
            return false;
        }

        public string Join(IIterable<string> iterable)
        {
            return _s;
        }

        public void LJust(uint width, string fillchar)
        {
        }

        public string Lower()
        {
            return _s.ToLower();
        }

        public void LStrip(string chars)
        {
        }

        public void MakeTrans(IMapping<string, string?> mapping)
        {
        }

        public void MakeTrans(IMapping<uint, string?> mapping)
        {
        }

        public void MakeTrans(string fromChars, string toChars)
        {
        }

        public void MakeTrans(string fromChars, string toChars, string ignoreChars)
        {
        }

        public void Partition(string sep)
        {
        }

        public void RemovePrefix(string prefix)
        {
        }

        public void RemoveSuffix(string suffix)
        {
        }

        public void Replace(string old, string @new, uint count)
        {
        }

        public void RFind(string sub, int start = 0, int end = -1)
        {
        }

        public void RIndex(string sub, int start = 0, int end = -1)
        {
        }

        public void RJust(uint width, string fillchar)
        {
        }

        public void RPartition(string sep)
        {
        }

        public void RSplit(string sep, uint maxsplit)
        {
        }

        public void RStrip(string chars)
        {
        }

        public void Split(string sep, uint maxsplit)
        {
        }

        public void SplitLines(bool keepends = false)
        {
        }

        public void StartsWith(string prefix, int start = 0, int end = -1)
        {
        }

        public void Strip(string chars)
        {
        }

        public void SwapCase()
        {
        }

        public void Title()
        {
        }

        public void Translate(uint table)
        {
        }

        public Str Upper()
        {
            return _s.ToUpper();
        }

        public void ZFill(uint width)
        {
        }
    }
}
