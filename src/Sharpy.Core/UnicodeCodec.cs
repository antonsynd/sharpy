using System;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// The codec half of <c>str.encode</c> and <c>bytes.decode</c>: python's codec names, its
    /// <c>errors</c> handlers (<c>strict</c>, <c>replace</c>, <c>ignore</c>) and its
    /// <see cref="UnicodeEncodeError"/>/<see cref="UnicodeDecodeError"/> fields and reasons (#2477).
    /// .NET's encoders do the byte work; this class decides which characters or bytes are an error,
    /// because .NET's fallbacks substitute silently and report one position where python reports a run.
    /// </summary>
    internal static class UnicodeCodec
    {
        private enum Codec
        {
            Utf8,
            Ascii,
            Latin1,
            Utf16Le,
            Utf16Be,
            Utf32Le,
        }

        private enum Handler
        {
            Strict,
            Replace,
            Ignore,
        }

        private static readonly Encoding s_strictUtf8 = new UTF8Encoding(false, true);

        private static readonly Encoding s_latin1 = Encoding.GetEncoding("iso-8859-1");

        /// <summary>Python: <c>str.encode(encoding, errors)</c>.</summary>
        internal static Bytes Encode(string s, string encoding, string errors)
        {
            Codec codec = Resolve(encoding, out string pythonName, out _);
            int bad = NextUnencodable(codec, s, 0);
            if (bad < 0)
            {
                return new Bytes(GetEncoding(codec).GetBytes(s));
            }

            // Python resolves the handler only when an error occurs ('x'.encode('ascii', 'bogus') is b'x').
            Handler handler = ResolveHandler(errors);
            if (handler == Handler.Strict)
            {
                int end = bad + Width(s, bad);
                while (end < s.Length && !IsEncodable(codec, s, end))
                {
                    end += Width(s, end);
                }
                throw new UnicodeEncodeError(pythonName, s, bad, end, EncodeReason(codec));
            }

            var kept = new StringBuilder(s.Length);
            int i = 0;
            while (i < s.Length)
            {
                int width = Width(s, i);
                if (IsEncodable(codec, s, i))
                {
                    kept.Append(s, i, width);
                }
                else if (handler == Handler.Replace)
                {
                    kept.Append('?');
                }
                i += width;
            }
            return new Bytes(GetEncoding(codec).GetBytes(kept.ToString()));
        }

        /// <summary>Python: <c>bytes.decode(encoding, errors)</c>.</summary>
        internal static string Decode(Bytes self, byte[] data, string encoding, string errors)
        {
            Codec codec = Resolve(encoding, out _, out string pythonName);
            switch (codec)
            {
                case Codec.Latin1:
                    return s_latin1.GetString(data);
                case Codec.Utf8:
                    try
                    {
                        return s_strictUtf8.GetString(data);
                    }
                    catch (DecoderFallbackException)
                    {
                        return DecodeScanned(self, data, codec, pythonName, errors);
                    }
                case Codec.Ascii:
                    return DecodeScanned(self, data, codec, pythonName, errors);
                default:
                    return DecodeWide(self, data, codec, pythonName, errors);
            }
        }

        // UTF-8 and ASCII: find each invalid run as CPython's decoder does (maximal subparts), then
        // strict raises on the first, replace writes one U+FFFD per run and ignore drops it.
        private static string DecodeScanned(Bytes self, byte[] data, Codec codec, string pythonName, string errors)
        {
            var text = new StringBuilder(data.Length);
            Handler? handler = null;
            int segment = 0;
            int i = 0;
            while (i < data.Length)
            {
                int length = codec == Codec.Utf8
                    ? Utf8Invalid(data, i, out string reason)
                    : AsciiInvalid(data, i, out reason);
                if (length == 0)
                {
                    i++;
                    continue;
                }

                handler ??= ResolveHandler(errors);
                if (handler == Handler.Strict)
                {
                    throw new UnicodeDecodeError(pythonName, self, i, i + length, reason);
                }
                text.Append(GetEncoding(codec).GetString(data, segment, i - segment));
                if (handler == Handler.Replace)
                {
                    text.Append('�');
                }
                i += length;
                segment = i;
            }
            text.Append(GetEncoding(codec).GetString(data, segment, data.Length - segment));
            return text.ToString();
        }

        // UTF-16/UTF-32: .NET reports the failing bytes but not why, so the reason is python's
        // generic UTF-16 wording, "illegal encoding".
        private static string DecodeWide(Bytes self, byte[] data, Codec codec, string pythonName, string errors)
        {
            Encoding strict = Encoding.GetEncoding(
                GetEncoding(codec).CodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            try
            {
                return strict.GetString(data);
            }
            catch (DecoderFallbackException ex)
            {
                Handler handler = ResolveHandler(errors);
                if (handler == Handler.Strict)
                {
                    int start = ex.Index >= 0 && ex.Index < data.Length ? ex.Index : 0;
                    int length = ex.BytesUnknown != null && ex.BytesUnknown.Length > 0 ? ex.BytesUnknown.Length : 1;
                    throw new UnicodeDecodeError(pythonName, self, start, Math.Min(start + length, data.Length), "illegal encoding");
                }
                string replacement = handler == Handler.Replace ? "�" : "";
                Encoding lenient = Encoding.GetEncoding(
                    GetEncoding(codec).CodePage, EncoderFallback.ExceptionFallback, new DecoderReplacementFallback(replacement));
                return lenient.GetString(data);
            }
        }

        // Length of the invalid run starting at data[i], or 0 when a valid sequence starts there.
        // CPython's UTF-8 decoder: the run is the maximal subpart of an ill-formed sequence.
        private static int Utf8Invalid(byte[] data, int i, out string reason)
        {
            reason = "";
            byte lead = data[i];
            if (lead < 0x80)
            {
                return 0;
            }

            int continuations;
            byte secondLow = 0x80;
            byte secondHigh = 0xBF;
            if (lead >= 0xC2 && lead <= 0xDF)
            {
                continuations = 1;
            }
            else if (lead >= 0xE0 && lead <= 0xEF)
            {
                continuations = 2;
                if (lead == 0xE0)
                {
                    secondLow = 0xA0;
                }
                else if (lead == 0xED)
                {
                    secondHigh = 0x9F;
                }
            }
            else if (lead >= 0xF0 && lead <= 0xF4)
            {
                continuations = 3;
                if (lead == 0xF0)
                {
                    secondLow = 0x90;
                }
                else if (lead == 0xF4)
                {
                    secondHigh = 0x8F;
                }
            }
            else
            {
                reason = "invalid start byte";
                return 1;
            }

            for (int k = 1; k <= continuations; k++)
            {
                int at = i + k;
                if (at >= data.Length)
                {
                    reason = "unexpected end of data";
                    return data.Length - i;
                }
                byte low = k == 1 ? secondLow : (byte)0x80;
                byte high = k == 1 ? secondHigh : (byte)0xBF;
                if (data[at] < low || data[at] > high)
                {
                    reason = "invalid continuation byte";
                    return k;
                }
            }
            return 0;
        }

        private static int AsciiInvalid(byte[] data, int i, out string reason)
        {
            reason = "ordinal not in range(128)";
            return data[i] < 0x80 ? 0 : 1;
        }

        private static int NextUnencodable(Codec codec, string s, int from)
        {
            for (int i = from; i < s.Length; i += Width(s, i))
            {
                if (!IsEncodable(codec, s, i))
                {
                    return i;
                }
            }
            return -1;
        }

        // A surrogate pair is one character (python's one code point); a lone surrogate is one.
        private static int Width(string s, int i)
        {
            return i + 1 < s.Length && char.IsSurrogatePair(s[i], s[i + 1]) ? 2 : 1;
        }

        private static bool IsEncodable(Codec codec, string s, int i)
        {
            char c = s[i];
            switch (codec)
            {
                case Codec.Ascii:
                    return c < 0x80;
                case Codec.Latin1:
                    return c < 0x100;
                default:
                    return !char.IsSurrogate(c) || Width(s, i) == 2;
            }
        }

        private static string EncodeReason(Codec codec)
        {
            switch (codec)
            {
                case Codec.Ascii:
                    return "ordinal not in range(128)";
                case Codec.Latin1:
                    return "ordinal not in range(256)";
                default:
                    return "surrogates not allowed";
            }
        }

        private static Encoding GetEncoding(Codec codec)
        {
            switch (codec)
            {
                case Codec.Utf8:
                    return Encoding.UTF8;
                case Codec.Ascii:
                    return Encoding.ASCII;
                case Codec.Latin1:
                    return s_latin1;
                case Codec.Utf16Le:
                    return Encoding.Unicode;
                case Codec.Utf16Be:
                    return Encoding.BigEndianUnicode;
                default:
                    return Encoding.UTF32;
            }
        }

        // Python's codec lookup ignores case, '-' and '_'. The encode and decode names differ for the
        // BOM-less defaults: python reports 'utf-16' when encoding and 'utf-16-le' when decoding.
        private static Codec Resolve(string encoding, out string encodeName, out string decodeName)
        {
#pragma warning disable CA1307
            switch (encoding.ToLowerInvariant().Replace("-", "").Replace("_", ""))
#pragma warning restore CA1307
            {
                case "utf8":
                    encodeName = decodeName = "utf-8";
                    return Codec.Utf8;
                case "ascii":
                    encodeName = decodeName = "ascii";
                    return Codec.Ascii;
                case "latin1":
                case "iso88591":
                    encodeName = decodeName = "latin-1";
                    return Codec.Latin1;
                case "utf16":
                    encodeName = "utf-16";
                    decodeName = "utf-16-le";
                    return Codec.Utf16Le;
                case "utf16le":
                    encodeName = decodeName = "utf-16-le";
                    return Codec.Utf16Le;
                case "utf16be":
                    encodeName = decodeName = "utf-16-be";
                    return Codec.Utf16Be;
                case "utf32":
                    encodeName = "utf-32";
                    decodeName = "utf-32-le";
                    return Codec.Utf32Le;
                default:
                    throw new LookupError("unknown encoding: " + encoding);
            }
        }

        private static Handler ResolveHandler(string errors)
        {
            switch (errors)
            {
                case "strict":
                    return Handler.Strict;
                case "replace":
                    return Handler.Replace;
                case "ignore":
                    return Handler.Ignore;
                default:
                    throw new LookupError("unknown error handler name '" + errors + "'");
            }
        }
    }
}
