using System;
using System.Globalization;

namespace Sharpy
{
    /// <summary>
    /// Raised when a string cannot be encoded with a codec. Equivalent to Python's
    /// <c>UnicodeEncodeError</c>: a <see cref="UnicodeError"/> (and so <see cref="ValueError"/>)
    /// subclass carrying python's five fields (#2124, #2477). Positions are UTF-16 code-unit
    /// indices into <see cref="Object"/>, as every Sharpy string index is.
    /// </summary>
    public class UnicodeEncodeError : UnicodeError
    {
        /// <summary>
        /// Create a UnicodeEncodeError for <c>obj[start:end]</c>, which the codec named
        /// <paramref name="encoding"/> could not encode. Python:
        /// <c>UnicodeEncodeError(encoding, object, start, end, reason)</c>.
        /// </summary>
        public UnicodeEncodeError(string encoding, string obj, int start, int end, string reason)
            : base(FormatMessage(encoding, obj, start, end, reason))
        {
            Encoding = encoding;
            Object = obj;
            Start = start;
            End = end;
            Reason = reason;
        }

        /// <summary>The name of the codec that raised the error. Python: <c>e.encoding</c>.</summary>
        public string Encoding { get; }

        /// <summary>The string the codec was encoding. Python: <c>e.object</c>.</summary>
        public string Object { get; }

        /// <summary>The first index of the unencodable run. Python: <c>e.start</c>.</summary>
        public int Start { get; }

        /// <summary>One past the last index of the unencodable run. Python: <c>e.end</c>.</summary>
        public int End { get; }

        /// <summary>Why the codec failed. Python: <c>e.reason</c>.</summary>
        public string Reason { get; }

        // CPython's UnicodeEncodeError.__str__: one character names it, a run names its range.
        // A surrogate pair is one character (python's one code point) although it spans two indices.
        private static string FormatMessage(string encoding, string obj, int start, int end, string reason)
        {
            if (start >= 0 && start < obj.Length && end > start)
            {
                int width = end - start;
                if (width == 1)
                {
                    return string.Format(CultureInfo.InvariantCulture,
                        "'{0}' codec can't encode character '{1}' in position {2}: {3}",
                        encoding, EscapeCodePoint(obj[start]), start, reason);
                }
                if (width == 2 && char.IsSurrogatePair(obj[start], obj[start + 1]))
                {
                    return string.Format(CultureInfo.InvariantCulture,
                        "'{0}' codec can't encode character '{1}' in position {2}: {3}",
                        encoding, EscapeCodePoint(char.ConvertToUtf32(obj[start], obj[start + 1])), start, reason);
                }
            }
            return string.Format(CultureInfo.InvariantCulture,
                "'{0}' codec can't encode characters in position {1}-{2}: {3}",
                encoding, start, end - 1, reason);
        }

        private static string EscapeCodePoint(int cp)
        {
            if (cp < 0x100)
            {
                return "\\x" + cp.ToString("x2", CultureInfo.InvariantCulture);
            }
            if (cp < 0x10000)
            {
                return "\\u" + cp.ToString("x4", CultureInfo.InvariantCulture);
            }
            return "\\U" + cp.ToString("x8", CultureInfo.InvariantCulture);
        }
    }
}
