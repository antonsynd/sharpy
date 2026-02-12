using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System;
namespace Sharpy
{
    using System.Text;

    public static partial class Builtins
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
            if (obj is Str str)
            {
                return _s == str._s;
            }

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

        /// <summary>
        /// Return a casefolded copy of the string. Casefolded strings may be used for caseless matching.
        /// </summary>
        public Str CaseFold()
        {
            return new Str(_s.ToLowerInvariant());
        }

        /// <summary>
        /// Return centered in a string of length width. Padding is done using the specified fill character (default is a space).
        /// </summary>
        public Str Center(uint width, Str fillchar = default)
        {
            var fill = fillchar._s ?? " ";
            if (fill.Length != 1)
            {
                throw new ValueError("The fill character must be exactly one character long");
            }

            var len = _s.Length;
            if (len >= width)
            {
                return this;
            }

            var totalPadding = (int)width - len;
            var leftPadding = totalPadding / 2;
            var rightPadding = totalPadding - leftPadding;

            return new Str(new string(fill[0], leftPadding) + _s + new string(fill[0], rightPadding));
        }

        /// <summary>
        /// Return the number of non-overlapping occurrences of substring sub in the range [start, end].
        /// </summary>
        public int Count(Str sub, int start = 0, int? end = null)
        {
            var (actualStart, actualEnd) = NormalizeSliceIndices(start, end);
            if (actualStart >= actualEnd)
                return 0;

            var substring = _s.Substring(actualStart, actualEnd - actualStart);
            var subStr = (string)sub;

            if (string.IsNullOrEmpty(subStr))
            {
                return substring.Length + 1; // Python behavior: empty string occurs before, between, and after every character
            }

            int count = 0;
            int index = 0;
            while ((index = substring.IndexOf(subStr, index)) >= 0)
            {
                count++;
                index += subStr.Length; // Move past this occurrence
            }

            return count;
        }

        /// <summary>
        /// Encode the string to bytes using the specified encoding.
        /// </summary>
        /// <param name="encoding">The encoding to use (default: "utf-8")</param>
        /// <param name="errors">Error handling scheme (default: "strict")</param>
        /// <returns>Bytes representation of the string</returns>
        public Bytes Encode(string encoding = "utf-8", string errors = "strict")
        {
            Encoding enc = encoding.ToLowerInvariant() switch
            {
                "utf-8" or "utf8" => Encoding.UTF8,
                "utf-16" or "utf16" => Encoding.Unicode,
                "utf-32" or "utf32" => Encoding.UTF32,
                "ascii" => Encoding.ASCII,
                "latin-1" or "latin1" or "iso-8859-1" => Encoding.GetEncoding("iso-8859-1"),
                _ => throw new ValueError($"Unknown encoding: {encoding}")
            };

            try
            {
                byte[] bytes = enc.GetBytes(_s);
                return new Bytes(bytes);
            }
            catch (EncoderFallbackException ex)
            {
                if (errors == "strict")
                {
                    throw new UnicodeEncodeError($"Cannot encode character: {ex.Message}");
                }
                else if (errors == "ignore")
                {
                    // Encode with replacement fallback that skips unencodable characters
                    var encoderFallback = new EncoderReplacementFallback(string.Empty);
                    var encodingWithFallback = (Encoding)enc.Clone();
                    encodingWithFallback.EncoderFallback = encoderFallback;
                    byte[] bytes = encodingWithFallback.GetBytes(_s);
                    return new Bytes(bytes);
                }
                else if (errors == "replace")
                {
                    // Encode with '?' replacement for unencodable characters (default behavior)
                    byte[] bytes = enc.GetBytes(_s);
                    return new Bytes(bytes);
                }
                else
                {
                    throw new ValueError($"Unknown error handling scheme: {errors}");
                }
            }
        }

        /// <summary>
        /// Return True if the string ends with the specified suffix, otherwise return False.
        /// </summary>
        public bool EndsWith(Str suffix, int start = 0, int? end = null)
        {
            var (actualStart, actualEnd) = NormalizeSliceIndices(start, end);
            if (actualStart >= actualEnd)
                return false;

            var substring = _s.Substring(actualStart, actualEnd - actualStart);
            return substring.EndsWith((string)suffix);
        }

        /// <summary>
        /// Return a copy of the string where all tab characters are replaced by one or more spaces,
        /// depending on the current column and the given tab size. Tab positions occur every tabsize characters (default is 8).
        /// </summary>
        public Str ExpandTabs(int tabsize = 8)
        {
            if (tabsize < 0)
            {
                throw new ValueError("tabsize must be non-negative");
            }

            if (!_s.Contains('\t'))
            {
                return this;
            }

            // Handle tabsize == 0: remove all tabs
            if (tabsize == 0)
            {
                return new Str(_s.Replace("\t", ""));
            }

            var result = new StringBuilder(_s.Length);
            int column = 0;

            foreach (char c in _s)
            {
                if (c == '\t')
                {
                    // Calculate spaces needed to reach next tab stop
                    var spacesNeeded = tabsize - (column % tabsize);
                    result.Append(new string(' ', spacesNeeded));
                    column += spacesNeeded;
                }
                else if (c == '\n' || c == '\r')
                {
                    result.Append(c);
                    column = 0;
                }
                else
                {
                    result.Append(c);
                    column++;
                }
            }

            return new Str(result.ToString());
        }

        /// <summary>
        /// Return the lowest index in the string where substring sub is found within the slice s[start:end].
        /// Return -1 if sub is not found.
        /// </summary>
        public int Find(Str sub, int start = 0, int? end = null)
        {
            var (actualStart, actualEnd) = NormalizeSliceIndices(start, end);
            if (actualStart >= actualEnd)
                return -1;

            var substring = _s.Substring(actualStart, actualEnd - actualStart);
            var index = substring.IndexOf((string)sub);

            return index >= 0 ? actualStart + index : -1;
        }

        /// <summary>
        /// Perform a string formatting operation. The string on which this method is called can contain literal text
        /// or replacement fields delimited by braces {}. Each replacement field contains either the numeric index of a
        /// positional argument, or the name of a keyword argument.
        /// See: #108 (full implementation requires Python format string parser)
        /// </summary>
        public Str Format(params object[] args)
        {
            // See: #108
            throw new NotImplementedException("str.format() requires full format string parser - planned for v0.6");
        }

        /// <summary>
        /// Similar to Format(**mapping), except that mapping is used directly and not copied to a dict.
        /// This is useful if for example mapping is a dict subclass.
        /// See: #108 (full implementation requires Python format string parser)
        /// </summary>
        public Str FormatMap<K, V>(IReadOnlyDictionary<K, V> mapping) where K : notnull
        {
            // See: #108
            throw new NotImplementedException("str.format_map() requires full format string parser - planned for v0.6");
        }

        /// <summary>
        /// Like Find(), but raise ValueError when the substring is not found.
        /// </summary>
        public int Index(Str sub, int start = 0, int? end = null)
        {
            var index = Find(sub, start, end);
            if (index < 0)
            {
                throw new ValueError($"substring '{(string)sub}' not found");
            }
            return index;
        }

        /// <summary>
        /// Return True if all characters in the string are alphanumeric and there is at least one character, False otherwise.
        /// </summary>
        public bool IsAlnum()
        {
            if (string.IsNullOrEmpty(_s))
            {
                return false;
            }

            return _s.All(char.IsLetterOrDigit);
        }

        /// <summary>
        /// Return True if all characters in the string are alphabetic and there is at least one character, False otherwise.
        /// </summary>
        public bool IsAlpha()
        {
            if (string.IsNullOrEmpty(_s))
            {
                return false;
            }

            return _s.All(char.IsLetter);
        }

        /// <summary>
        /// Return True if all characters in the string are ASCII, False otherwise.
        /// ASCII characters have code points in the range U+0000-U+007F.
        /// </summary>
        public bool IsAscii()
        {
            if (string.IsNullOrEmpty(_s))
            {
                return true; // Empty string is considered ASCII
            }

            return _s.All(c => c <= 127);
        }

        /// <summary>
        /// Return True if all characters in the string are digits and there is at least one character, False otherwise.
        /// </summary>
        public bool IsDigit()
        {
            if (string.IsNullOrEmpty(_s))
            {
                return false;
            }

            return _s.All(char.IsDigit);
        }

        /// <summary>
        /// Return True if all characters in the string are decimal characters, and there is at least one character, False otherwise.
        /// Decimal characters are those that can be used to form numbers in base 10.
        /// </summary>
        public bool IsDecimal()
        {
            if (string.IsNullOrEmpty(_s))
            {
                return false;
            }

            return _s.All(c => char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.DecimalDigitNumber);
        }

        /// <summary>
        /// Return True if the string is a valid identifier according to Python language definition.
        /// </summary>
        public bool IsIdentifier()
        {
            if (string.IsNullOrEmpty(_s))
            {
                return false;
            }

            // First character must be letter or underscore
            if (!char.IsLetter(_s[0]) && _s[0] != '_')
            {
                return false;
            }

            // Rest must be letters, digits, or underscores
            for (int i = 1; i < _s.Length; i++)
            {
                if (!char.IsLetterOrDigit(_s[i]) && _s[i] != '_')
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Return True if all cased characters in the string are lowercase and there is at least one cased character, False otherwise.
        /// </summary>
        public bool IsLower()
        {
            if (string.IsNullOrEmpty(_s))
            {
                return false;
            }

            bool hasCased = false;
            foreach (char c in _s)
            {
                if (char.IsUpper(c))
                {
                    return false;
                }
                if (char.IsLower(c))
                {
                    hasCased = true;
                }
            }

            return hasCased;
        }

        /// <summary>
        /// Return True if all characters in the string are numeric characters, and there is at least one character, False otherwise.
        /// Numeric characters include digit characters, and all characters that have the Unicode numeric value property.
        /// </summary>
        public bool IsNumeric()
        {
            if (string.IsNullOrEmpty(_s))
            {
                return false;
            }

            return _s.All(c => char.IsNumber(c));
        }

        /// <summary>
        /// Return True if all characters in the string are printable or the string is empty, False otherwise.
        /// Nonprintable characters are those characters defined in the Unicode character database as "Other" or "Separator",
        /// excepting the ASCII space (0x20) which is considered printable.
        /// </summary>
        public bool IsPrintable()
        {
            if (string.IsNullOrEmpty(_s))
            {
                return true; // Empty string is considered printable
            }

            foreach (char c in _s)
            {
                var category = char.GetUnicodeCategory(c);

                // ASCII space is printable
                if (c == ' ')
                {
                    continue;
                }

                // Control characters, format characters, surrogate characters, private use, and non-assigned are not printable
                if (category == System.Globalization.UnicodeCategory.Control ||
                    category == System.Globalization.UnicodeCategory.Format ||
                    category == System.Globalization.UnicodeCategory.Surrogate ||
                    category == System.Globalization.UnicodeCategory.PrivateUse ||
                    category == System.Globalization.UnicodeCategory.OtherNotAssigned ||
                    category == System.Globalization.UnicodeCategory.LineSeparator ||
                    category == System.Globalization.UnicodeCategory.ParagraphSeparator)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Return True if all characters in the string are whitespace and there is at least one character, False otherwise.
        /// </summary>
        public bool IsSpace()
        {
            if (string.IsNullOrEmpty(_s))
            {
                return false;
            }

            return _s.All(char.IsWhiteSpace);
        }

        /// <summary>
        /// Return True if the string is a titlecased string and there is at least one character.
        /// </summary>
        public bool IsTitle()
        {
            if (string.IsNullOrEmpty(_s))
            {
                return false;
            }

            bool previousWasLetter = false;
            bool hasLetter = false;

            foreach (char c in _s)
            {
                if (char.IsUpper(c))
                {
                    if (previousWasLetter)
                    {
                        return false;
                    }
                    previousWasLetter = true;
                    hasLetter = true;
                }
                else if (char.IsLower(c))
                {
                    if (!previousWasLetter)
                    {
                        return false;
                    }
                    hasLetter = true;
                }
                else
                {
                    previousWasLetter = false;
                }
            }

            return hasLetter;
        }

        /// <summary>
        /// Return True if all cased characters in the string are uppercase and there is at least one cased character, False otherwise.
        /// </summary>
        public bool IsUpper()
        {
            if (string.IsNullOrEmpty(_s))
            {
                return false;
            }

            bool hasCased = false;
            foreach (char c in _s)
            {
                if (char.IsLower(c))
                {
                    return false;
                }
                if (char.IsUpper(c))
                {
                    hasCased = true;
                }
            }

            return hasCased;
        }

        /// <summary>
        /// Return a string which is the concatenation of the strings in iterable.
        /// A TypeError will be raised if there are any non-string values in iterable.
        /// The separator between elements is the string providing this method.
        /// </summary>
        public Str Join(IEnumerable<Str> iterable)
        {
            if (iterable is null)
            {
                throw TypeError.IsNotInterface("NoneType", "iterable");
            }

            return new Str(string.Join(_s, iterable.Select(item => (string)item)));
        }

        /// <summary>
        /// Return a left-justified string of length width. Padding is done using the specified fill character (default is a space).
        /// </summary>
        public Str LJust(uint width, string fillchar = " ")
        {
            if (fillchar.Length != 1)
            {
                throw new TypeError("The fill character must be exactly one character long");
            }

            if (_s.Length >= width)
            {
                return this;
            }

            return new Str(_s.PadRight((int)width, fillchar[0]));
        }

        /// <summary>
        /// Return a copy of the string converted to lowercase.
        /// </summary>
        public Str Lower()
        {
            return new Str(_s.ToLower());
        }

        /// <summary>
        /// Return a copy of the string with leading characters removed.
        /// The chars argument is a string specifying the set of characters to be removed.
        /// If omitted or None, the chars argument defaults to removing whitespace.
        /// </summary>
        public Str LStrip(Str? chars = null)
        {
            if (chars is null)
            {
                return new Str(_s.TrimStart());
            }

            return new Str(_s.TrimStart(((string)chars).ToCharArray()));
        }

        /// <summary>
        /// Static method to create a translation table usable for Translate().
        /// This version accepts a dictionary mapping strings to their replacements.
        /// </summary>
        public static Dict<uint, Str?> MakeTrans(IReadOnlyDictionary<Str, Str?> mapping)
        {
            if (mapping is null)
            {
                throw new ArgumentNullException(nameof(mapping));
            }

            var table = new Dict<uint, Str?>();

            foreach (var key in mapping.Keys)
            {
                var keyStr = (string)key;
                if (keyStr.Length != 1)
                {
                    throw new ValueError("string keys in translate table must be of length 1");
                }

                var codePoint = (uint)keyStr[0];
                var value = mapping[key];
                table[codePoint] = value;
            }

            return table;
        }

        /// <summary>
        /// Static method to create a translation table usable for Translate().
        /// This version accepts a dictionary mapping Unicode code points to their replacements.
        /// </summary>
        public static Dict<uint, Str?> MakeTrans(IReadOnlyDictionary<uint, Str?> mapping)
        {
            var table = new Dict<uint, Str?>();

            foreach (var key in mapping.Keys)
            {
                table[key] = mapping[key];
            }

            return table;
        }

        /// <summary>
        /// Static method to create a translation table usable for Translate().
        /// Maps characters in fromChars to characters in toChars at corresponding positions.
        /// </summary>
        public static Dict<uint, Str?> MakeTrans(Str fromChars, Str toChars)
        {
            if (fromChars._s is null)
            {
                throw new ArgumentNullException(nameof(fromChars));
            }

            if (toChars._s is null)
            {
                throw new ArgumentNullException(nameof(toChars));
            }

            var from = (string)fromChars;
            var to = (string)toChars;

            if (from.Length != to.Length)
            {
                throw new ValueError("the first two maketrans arguments must have equal length");
            }

            var table = new Dict<uint, Str?>();
            for (int i = 0; i < from.Length; i++)
            {
                table[(uint)from[i]] = new Str(to[i]);
            }

            return table;
        }

        /// <summary>
        /// Static method to create a translation table usable for Translate().
        /// Maps characters in fromChars to characters in toChars at corresponding positions,
        /// and maps characters in ignoreChars to None (deletion).
        /// </summary>
        public static Dict<uint, Str?> MakeTrans(Str fromChars, Str toChars, Str ignoreChars)
        {
            if (fromChars._s is null)
            {
                throw new ArgumentNullException(nameof(fromChars));
            }

            if (toChars._s is null)
            {
                throw new ArgumentNullException(nameof(toChars));
            }

            if (ignoreChars._s is null)
            {
                throw new ArgumentNullException(nameof(ignoreChars));
            }

            var from = (string)fromChars;
            var to = (string)toChars;
            var ignore = (string)ignoreChars;

            if (from.Length != to.Length)
            {
                throw new ValueError("the first two maketrans arguments must have equal length");
            }

            var table = new Dict<uint, Str?>();

            // Map fromChars to toChars
            for (int i = 0; i < from.Length; i++)
            {
                table[(uint)from[i]] = new Str(to[i]);
            }

            // Map ignoreChars to None for deletion
            foreach (char c in ignore)
            {
                table[(uint)c] = null;
            }

            return table;
        }

        /// <summary>
        /// Split the string at the first occurrence of sep, and return a 3-tuple containing the part before the separator,
        /// the separator itself, and the part after the separator. If the separator is not found, return a 3-tuple containing
        /// the string itself, followed by two empty strings.
        /// </summary>
        public (Str, Str, Str) Partition(Str sep)
        {
            if (sep._s is null)
            {
                throw new ArgumentNullException(nameof(sep));
            }

            if (string.IsNullOrEmpty((string)sep))
            {
                throw new ValueError("empty separator");
            }

            var index = _s.IndexOf((string)sep);
            if (index < 0)
            {
                return (this, "", "");
            }

            var before = new Str(_s.Substring(0, index));
            var after = new Str(_s.Substring(index + ((string)sep).Length));
            return (before, sep, after);
        }

        /// <summary>
        /// If the string starts with the prefix string, return string[len(prefix):]. Otherwise, return a copy of the original string.
        /// </summary>
        public Str RemovePrefix(Str prefix)
        {
            if (_s.StartsWith((string)prefix))
            {
                return new Str(_s.Substring(((string)prefix).Length));
            }
            return this;
        }

        /// <summary>
        /// If the string ends with the suffix string, return string[:-len(suffix)]. Otherwise, return a copy of the original string.
        /// </summary>
        public Str RemoveSuffix(Str suffix)
        {
            if (_s.EndsWith((string)suffix))
            {
                return new Str(_s.Substring(0, _s.Length - ((string)suffix).Length));
            }
            return this;
        }

        /// <summary>
        /// Return a copy of the string with all occurrences of substring old replaced by new.
        /// If the optional argument count is given, only the first count occurrences are replaced.
        /// </summary>
        public Str Replace(Str old, Str @new, int count = -1)
        {
            if (count == 0)
            {
                return this;
            }

            if (count < 0)
            {
                return new Str(_s.Replace((string)old, (string)@new));
            }

            // Replace only the first 'count' occurrences
            var oldStr = (string)old;
            var newStr = (string)@new;
            var builder = new StringBuilder(_s);

            int replacements = 0;
            int searchIndex = 0;

            while (replacements < count && searchIndex < builder.Length)
            {
                var index = builder.ToString().IndexOf(oldStr, searchIndex);
                if (index < 0)
                {
                    break;
                }

                builder.Remove(index, oldStr.Length);
                builder.Insert(index, newStr);
                searchIndex = index + newStr.Length;
                replacements++;
            }

            return new Str(builder.ToString());
        }

        /// <summary>
        /// Return the highest index in the string where substring sub is found, such that sub is contained within s[start:end].
        /// Optional arguments start and end are interpreted as in slice notation. Return -1 on failure.
        /// </summary>
        public int RFind(Str? sub, int start = 0, int? end = null)
        {
            if (sub is null)
            {
                throw new ArgumentNullException(nameof(sub));
            }

            if (string.IsNullOrEmpty((string)sub))
            {
                throw new ValueError("empty substring");
            }

            var (actualStart, actualEnd) = NormalizeSliceIndices(start, end);
            if (actualStart >= actualEnd)
                return -1;

            var substring = _s.Substring(actualStart, actualEnd - actualStart);
            var index = substring.LastIndexOf((string)sub);

            return index >= 0 ? actualStart + index : -1;
        }

        /// <summary>
        /// Like RFind(), but raise ValueError when the substring is not found.
        /// </summary>
        public int RIndex(Str? sub, int start = 0, int? end = null)
        {
            if (sub is null)
            {
                throw new ArgumentNullException(nameof(sub));
            }

            if (string.IsNullOrEmpty((string)sub))
            {
                throw new ValueError("empty substring");
            }

            var index = RFind(sub, start, end);
            if (index < 0)
            {
                throw new ValueError($"substring '{(string)sub}' not found");
            }
            return index;
        }

        /// <summary>
        /// Return a right-justified string of length width. Padding is done using the specified fill character (default is a space).
        /// </summary>
        public Str RJust(uint width, string fillchar = " ")
        {
            if (fillchar is null)
            {
                throw new ArgumentNullException(nameof(fillchar));
            }

            if (fillchar.Length != 1)
            {
                throw new TypeError("The fill character must be exactly one character long");
            }

            if (_s.Length >= width)
            {
                return this;
            }

            return new Str(_s.PadLeft((int)width, fillchar[0]));
        }

        /// <summary>
        /// Split the string at the last occurrence of sep, and return a 3-tuple containing the part before the separator,
        /// the separator itself, and the part after the separator. If the separator is not found, return a 3-tuple containing
        /// two empty strings, followed by the string itself.
        /// </summary>
        public (Str, Str, Str) RPartition(Str sep)
        {
            if (sep._s is null)
            {
                throw new ArgumentNullException(nameof(sep));
            }

            if (string.IsNullOrEmpty((string)sep))
            {
                throw new ValueError("empty separator");
            }

            var index = _s.LastIndexOf((string)sep);
            if (index < 0)
            {
                return ("", "", this);
            }

            var before = new Str(_s.Substring(0, index));
            var after = new Str(_s.Substring(index + ((string)sep).Length));
            return (before, sep, after);
        }

        /// <summary>
        /// Return a list of the words in the string, using sep as the delimiter string, splitting from the right.
        /// If maxsplit is given, at most maxsplit splits are done (the rightmost ones).
        /// </summary>
        public List<Str> RSplit(Str? sep = null, int maxsplit = -1)
        {
            var result = new List<Str>();

            if (sep is null)
            {
                // Split on whitespace from right
                var parts = _s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

                if (maxsplit < 0 || maxsplit >= parts.Length - 1)
                {
                    foreach (var part in parts)
                    {
                        result.Add(new Str(part));
                    }
                }
                else
                {
                    // Need to keep the first parts together, preserving original whitespace
                    var keepTogether = parts.Length - maxsplit;
                    // Find the position in the original string where the last kept part ends
                    int endPos = 0;
                    for (int i = 0; i < keepTogether; i++)
                    {
                        int partIndex = _s.IndexOf(parts[i], endPos);
                        endPos = partIndex + parts[i].Length;
                    }
                    // Extract substring up to this position, then trim trailing whitespace
                    var combined = _s.Substring(0, endPos).TrimEnd();
                    result.Add(new Str(combined));

                    foreach (var part in parts.Skip(keepTogether))
                    {
                        result.Add(new Str(part));
                    }
                }
            }
            else
            {
                var sepStr = (string)sep;
                if (string.IsNullOrEmpty(sepStr))
                {
                    throw new ValueError("empty separator");
                }

                var parts = _s.Split(new[] { sepStr }, StringSplitOptions.None);

                if (maxsplit < 0 || maxsplit >= parts.Length - 1)
                {
                    foreach (var part in parts)
                    {
                        result.Add(new Str(part));
                    }
                }
                else
                {
                    // Need to keep the first parts together
                    var keepTogether = parts.Length - maxsplit;
                    var combined = string.Join(sepStr, parts.Take(keepTogether));
                    result.Add(new Str(combined));

                    foreach (var part in parts.Skip(keepTogether))
                    {
                        result.Add(new Str(part));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Return a copy of the string with trailing characters removed.
        /// The chars argument is a string specifying the set of characters to be removed.
        /// If omitted or None, the chars argument defaults to removing whitespace.
        /// </summary>
        public Str RStrip(Str? chars = null)
        {
            if (chars is null)
            {
                return new Str(_s.TrimEnd());
            }

            return new Str(_s.TrimEnd(((string)chars).ToCharArray()));
        }

        /// <summary>
        /// Return a list of the words in the string, using sep as the delimiter string.
        /// If maxsplit is given, at most maxsplit splits are done (thus, the list will
        /// have at most maxsplit+1 elements). If maxsplit is not specified or -1,
        /// then there is no limit on the number of splits (all possible splits are made).
        /// </summary>
        public List<Str> Split(Str? sep = null, int maxsplit = -1)
        {
            var result = new List<Str>();

            if (sep is null)
            {
                // Split on whitespace
                var parts = maxsplit < 0
                    ? _s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                    : _s.Split((char[]?)null, maxsplit + 1, StringSplitOptions.RemoveEmptyEntries);

                foreach (var part in parts)
                {
                    result.Add(new Str(part));
                }
            }
            else
            {
                var parts = maxsplit < 0
                    ? _s.Split(new[] { (string)sep }, StringSplitOptions.None)
                    : _s.Split(new[] { (string)sep }, maxsplit + 1, StringSplitOptions.None);

                foreach (var part in parts)
                {
                    result.Add(new Str(part));
                }
            }

            return result;
        }

        /// <summary>
        /// Return a list of the lines in the string, breaking at line boundaries.
        /// Line breaks are not included in the resulting list unless keepends is given and true.
        /// </summary>
        public List<Str> SplitLines(bool keepends = false)
        {
            var result = new List<Str>();
            if (string.IsNullOrEmpty(_s))
            {
                return result;
            }

            var currentLine = new StringBuilder();
            for (int i = 0; i < _s.Length; i++)
            {
                char c = _s[i];

                // Check for line boundaries
                if (c == '\n')
                {
                    if (keepends)
                    {
                        currentLine.Append(c);
                    }
                    result.Add(new Str(currentLine.ToString()));
                    currentLine.Clear();
                }
                else if (c == '\r')
                {
                    // Handle \r\n and \r
                    if (i + 1 < _s.Length && _s[i + 1] == '\n')
                    {
                        if (keepends)
                        {
                            currentLine.Append("\r\n");
                        }
                        result.Add(new Str(currentLine.ToString()));
                        currentLine.Clear();
                        i++; // Skip the \n
                    }
                    else
                    {
                        if (keepends)
                        {
                            currentLine.Append(c);
                        }
                        result.Add(new Str(currentLine.ToString()));
                        currentLine.Clear();
                    }
                }
                else
                {
                    currentLine.Append(c);
                }
            }

            // Add remaining content if any
            if (currentLine.Length > 0)
            {
                result.Add(new Str(currentLine.ToString()));
            }

            return result;
        }

        /// <summary>
        /// Return True if string starts with the prefix, otherwise return False.
        /// </summary>
        public bool StartsWith(Str prefix, int start = 0, int? end = null)
        {
            var (actualStart, actualEnd) = NormalizeSliceIndices(start, end);
            if (actualStart >= actualEnd)
                return false;

            var substring = _s.Substring(actualStart, actualEnd - actualStart);
            return substring.StartsWith((string)prefix);
        }

        /// <summary>
        /// Helper method to normalize slice indices for string methods.
        /// </summary>
        private (int start, int end) NormalizeSliceIndices(int start, int? end)
        {
            var actualEnd = end ?? _s.Length;
            if (start < 0)
                start = 0;
            if (actualEnd > _s.Length)
                actualEnd = _s.Length;
            if (actualEnd < 0)
                actualEnd = 0;
            return (start, actualEnd);
        }

        /// <summary>
        /// Return a copy of the string with leading and trailing characters removed.
        /// The chars argument is a string specifying the set of characters to be removed.
        /// If omitted or None, the chars argument defaults to removing whitespace.
        /// </summary>
        public Str Strip(Str? chars = null)
        {
            if (chars is null)
            {
                return new Str(_s.Trim());
            }

            return new Str(_s.Trim(((string)chars).ToCharArray()));
        }

        /// <summary>
        /// Return a copy of the string with uppercase characters converted to lowercase and vice versa.
        /// </summary>
        public Str SwapCase()
        {
            if (string.IsNullOrEmpty(_s))
            {
                return this;
            }

            var result = new StringBuilder(_s.Length);
            foreach (char c in _s)
            {
                if (char.IsUpper(c))
                {
                    result.Append(char.ToLower(c));
                }
                else if (char.IsLower(c))
                {
                    result.Append(char.ToUpper(c));
                }
                else
                {
                    result.Append(c);
                }
            }

            return new Str(result.ToString());
        }

        /// <summary>
        /// Return a titlecased version of the string where words start with an uppercase character
        /// and the remaining characters are lowercase.
        /// </summary>
        public Str Title()
        {
            if (string.IsNullOrEmpty(_s))
            {
                return this;
            }

            var result = new StringBuilder(_s.Length);
            bool previousWasLetter = false;

            foreach (char c in _s)
            {
                if (char.IsLetter(c))
                {
                    if (!previousWasLetter)
                    {
                        result.Append(char.ToUpper(c));
                    }
                    else
                    {
                        result.Append(char.ToLower(c));
                    }
                    previousWasLetter = true;
                }
                else
                {
                    result.Append(c);
                    previousWasLetter = false;
                }
            }

            return new Str(result.ToString());
        }

        /// <summary>
        /// Return a copy of the string in which each character has been mapped through the given translation table.
        /// The table must be a dictionary mapping Unicode ordinals (as integers) to Unicode ordinals, strings, or None.
        /// Unmapped characters are left untouched. Characters mapped to None are deleted.
        /// </summary>
        public Str Translate(Dict<uint, Str?> table)
        {
            if (table is null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            var result = new StringBuilder(_s.Length);

            foreach (char c in _s)
            {
                var codePoint = (uint)c;

                // Check if character is in translation table
                var replacement = table.Get(codePoint, null);

                if (replacement is null)
                {
                    // If mapped to None explicitly, delete the character
                    if (table.ContainsKey(codePoint))
                    {
                        continue; // Skip this character
                    }
                    else
                    {
                        // Not in table, keep original character
                        result.Append(c);
                    }
                }
                else
                {
                    // Replace with the mapped string
                    result.Append((string)replacement);
                }
            }

            return new Str(result.ToString());
        }

        /// <summary>
        /// Return a copy of the string converted to uppercase.
        /// </summary>
        public Str Upper()
        {
            return new Str(_s.ToUpper());
        }

        /// <summary>
        /// Return a copy of the string left filled with zeros. A leading sign prefix (+/-) is handled by inserting the padding after the sign.
        /// </summary>
        public Str ZFill(uint width)
        {
            if (_s.Length >= width)
            {
                return this;
            }

            var fillCount = (int)width - _s.Length;

            // Handle leading sign
            if (_s.Length > 0 && (_s[0] == '+' || _s[0] == '-'))
            {
                return new Str(_s[0] + new string('0', fillCount) + _s.Substring(1));
            }

            return new Str(new string('0', fillCount) + _s);
        }
    }
}
