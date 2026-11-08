using System;
using System.Globalization;

namespace Sharpy
{
    /// <summary>
    /// Raised when bytes cannot be decoded with a codec. Equivalent to Python's
    /// <c>UnicodeDecodeError</c>: a <see cref="UnicodeError"/> (and so <see cref="ValueError"/>)
    /// subclass carrying python's five fields (#2477).
    /// </summary>
    public class UnicodeDecodeError : UnicodeError
    {
        /// <summary>
        /// Create a UnicodeDecodeError for <c>obj[start:end]</c>, which the codec named
        /// <paramref name="encoding"/> could not decode. Python:
        /// <c>UnicodeDecodeError(encoding, object, start, end, reason)</c>.
        /// </summary>
        public UnicodeDecodeError(string encoding, Bytes obj, int start, int end, string reason)
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

        /// <summary>The bytes the codec was decoding. Python: <c>e.object</c>.</summary>
        public Bytes Object { get; }

        /// <summary>The first index of the undecodable run. Python: <c>e.start</c>.</summary>
        public int Start { get; }

        /// <summary>One past the last index of the undecodable run. Python: <c>e.end</c>.</summary>
        public int End { get; }

        /// <summary>Why the codec failed. Python: <c>e.reason</c>.</summary>
        public string Reason { get; }

        // CPython's UnicodeDecodeError.__str__: one byte names it, a run names its range.
        private static string FormatMessage(string encoding, Bytes obj, int start, int end, string reason)
        {
            if (start >= 0 && start < obj.Length && end == start + 1)
            {
                return string.Format(CultureInfo.InvariantCulture,
                    "'{0}' codec can't decode byte 0x{1:x2} in position {2}: {3}",
                    encoding, obj[start], start, reason);
            }
            return string.Format(CultureInfo.InvariantCulture,
                "'{0}' codec can't decode bytes in position {1}-{2}: {3}",
                encoding, start, end - 1, reason);
        }
    }
}
