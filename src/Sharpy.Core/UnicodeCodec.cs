using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// The codec half of <c>str.encode</c> and <c>bytes.decode</c> (#2477, #2495): python's codec names and
    /// aliases, its byte-order marks, its <c>errors</c> handlers (<c>strict</c>, <c>replace</c>, <c>ignore</c>,
    /// <c>backslashreplace</c>, <c>xmlcharrefreplace</c>) and its <see cref="UnicodeEncodeError"/>/
    /// <see cref="UnicodeDecodeError"/> fields and reasons. One resolver serves both directions. The strict
    /// <see cref="Encoding"/> instances do the byte work on valid input; only when they throw does this class
    /// walk the input to find each error as CPython's codec does, because .NET's fallbacks substitute per
    /// position where python reports a run, and report no reason.
    /// </summary>
    internal static class UnicodeCodec
    {
        private enum Codec
        {
            Utf8,
            Utf8Sig,
            Ascii,
            Latin1,
            Utf16,
            Utf16Le,
            Utf16Be,
            Utf32,
            Utf32Le,
            Utf32Be,
        }

        private enum Handler
        {
            Strict,
            Replace,
            Ignore,
            BackslashReplace,
            XmlCharRefReplace,
        }

        private static readonly Encoding s_utf8 = new UTF8Encoding(false, true);
        private static readonly Encoding s_ascii =
            Encoding.GetEncoding("us-ascii", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        private static readonly Encoding s_latin1 =
            Encoding.GetEncoding("iso-8859-1", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        private static readonly Encoding s_utf16Le = new UnicodeEncoding(false, false, true);
        private static readonly Encoding s_utf16Be = new UnicodeEncoding(true, false, true);
        private static readonly Encoding s_utf32Le = new UTF32Encoding(false, false, true);
        private static readonly Encoding s_utf32Be = new UTF32Encoding(true, false, true);

        private static readonly byte[] s_bomUtf8 = { 0xEF, 0xBB, 0xBF };
        private static readonly byte[] s_bomUtf16Le = { 0xFF, 0xFE };
        private static readonly byte[] s_bomUtf16Be = { 0xFE, 0xFF };
        private static readonly byte[] s_bomUtf32Le = { 0xFF, 0xFE, 0x00, 0x00 };
        private static readonly byte[] s_bomUtf32Be = { 0x00, 0x00, 0xFE, 0xFF };

        // Python's 'utf-16'/'utf-32' write and assume the machine's byte order.
        private static readonly Codec s_nativeUtf16 = BitConverter.IsLittleEndian ? Codec.Utf16Le : Codec.Utf16Be;
        private static readonly Codec s_nativeUtf32 = BitConverter.IsLittleEndian ? Codec.Utf32Le : Codec.Utf32Be;

        /// <summary>Python's codec module names (<c>encodings.&lt;module&gt;</c>) Sharpy implements.</summary>
        private static readonly Dictionary<string, Codec> s_modules = new Dictionary<string, Codec>(StringComparer.Ordinal)
        {
            ["utf_8"] = Codec.Utf8,
            ["utf_8_sig"] = Codec.Utf8Sig,
            ["ascii"] = Codec.Ascii,
            ["latin_1"] = Codec.Latin1,
            ["utf_16"] = Codec.Utf16,
            ["utf_16_le"] = Codec.Utf16Le,
            ["utf_16_be"] = Codec.Utf16Be,
            ["utf_32"] = Codec.Utf32,
            ["utf_32_le"] = Codec.Utf32Le,
            ["utf_32_be"] = Codec.Utf32Be,
        };

        /// <summary>The rows of python's <c>encodings.aliases.aliases</c> whose codec is in <see cref="s_modules"/>.</summary>
        private static readonly Dictionary<string, Codec> s_aliases = new Dictionary<string, Codec>(StringComparer.Ordinal)
        {
            ["646"] = Codec.Ascii,
            ["ansi_x3.4_1968"] = Codec.Ascii,
            ["ansi_x3.4_1986"] = Codec.Ascii,
            ["ansi_x3_4_1968"] = Codec.Ascii,
            ["cp367"] = Codec.Ascii,
            ["csascii"] = Codec.Ascii,
            ["ibm367"] = Codec.Ascii,
            ["iso646_us"] = Codec.Ascii,
            ["iso_646.irv_1991"] = Codec.Ascii,
            ["iso_ir_6"] = Codec.Ascii,
            ["us"] = Codec.Ascii,
            ["us_ascii"] = Codec.Ascii,
            ["8859"] = Codec.Latin1,
            ["cp819"] = Codec.Latin1,
            ["csisolatin1"] = Codec.Latin1,
            ["ibm819"] = Codec.Latin1,
            ["iso8859"] = Codec.Latin1,
            ["iso8859_1"] = Codec.Latin1,
            ["iso_8859_1"] = Codec.Latin1,
            ["iso_8859_1_1987"] = Codec.Latin1,
            ["iso_ir_100"] = Codec.Latin1,
            ["l1"] = Codec.Latin1,
            ["latin"] = Codec.Latin1,
            ["latin1"] = Codec.Latin1,
            ["csutf16"] = Codec.Utf16,
            ["u16"] = Codec.Utf16,
            ["utf16"] = Codec.Utf16,
            ["csutf16be"] = Codec.Utf16Be,
            ["unicodebigunmarked"] = Codec.Utf16Be,
            ["utf_16be"] = Codec.Utf16Be,
            ["csutf16le"] = Codec.Utf16Le,
            ["unicodelittleunmarked"] = Codec.Utf16Le,
            ["utf_16le"] = Codec.Utf16Le,
            ["csutf32"] = Codec.Utf32,
            ["u32"] = Codec.Utf32,
            ["utf32"] = Codec.Utf32,
            ["csutf32be"] = Codec.Utf32Be,
            ["utf_32be"] = Codec.Utf32Be,
            ["csutf32le"] = Codec.Utf32Le,
            ["utf_32le"] = Codec.Utf32Le,
            ["cp65001"] = Codec.Utf8,
            ["csutf8"] = Codec.Utf8,
            ["u8"] = Codec.Utf8,
            ["utf"] = Codec.Utf8,
            ["utf8"] = Codec.Utf8,
            ["utf8_ucs2"] = Codec.Utf8,
            ["utf8_ucs4"] = Codec.Utf8,
            ["utf8_sig"] = Codec.Utf8Sig,
        };

        /// <summary>
        /// Common spellings, matched as given before <see cref="Normalize"/> allocates: each resolves exactly as
        /// the normalised lookup would.
        /// </summary>
        private static readonly Dictionary<string, Codec> s_spellings = new Dictionary<string, Codec>(StringComparer.Ordinal)
        {
            ["utf-8"] = Codec.Utf8,
            ["utf8"] = Codec.Utf8,
            ["UTF-8"] = Codec.Utf8,
            ["UTF8"] = Codec.Utf8,
            ["utf_8"] = Codec.Utf8,
            ["utf-8-sig"] = Codec.Utf8Sig,
            ["ascii"] = Codec.Ascii,
            ["ASCII"] = Codec.Ascii,
            ["us-ascii"] = Codec.Ascii,
            ["latin-1"] = Codec.Latin1,
            ["latin1"] = Codec.Latin1,
            ["iso-8859-1"] = Codec.Latin1,
            ["utf-16"] = Codec.Utf16,
            ["utf-16-le"] = Codec.Utf16Le,
            ["utf-16-be"] = Codec.Utf16Be,
            ["utf-32"] = Codec.Utf32,
            ["utf-32-le"] = Codec.Utf32Le,
            ["utf-32-be"] = Codec.Utf32Be,
        };

        /// <summary>Python: <c>str.encode(encoding, errors)</c>.</summary>
        internal static Bytes Encode(string s, string encoding, string errors)
        {
            Codec codec = Resolve(encoding);
            Codec unit = ByteOrder(codec);
            Encoding target = GetEncoding(unit);
            byte[]? bom = EncodeBom(codec);
            if (!IsStrict(errors))
            {
                // A handler other than strict expects errors: walk once instead of throwing and walking.
                return EncodeWithErrors(s, codec, unit, target, bom, errors);
            }
            try
            {
                return Bytes.Wrap(GetBytes(target, bom, s));
            }
            catch (EncoderFallbackException)
            {
                return EncodeWithErrors(s, codec, unit, target, bom, errors);
            }
        }

        /// <summary>Python: <c>bytes.decode(encoding, errors)</c>.</summary>
        internal static string Decode(Bytes self, byte[] data, string encoding, string errors)
        {
            Codec codec = Resolve(encoding);
            int start = 0;
            Codec unit;
            switch (codec)
            {
                case Codec.Utf8Sig:
                    unit = Codec.Utf8;
                    start = StartsWith(data, s_bomUtf8) ? 3 : 0;
                    break;
                case Codec.Utf16:
                    unit = DetectBom(data, Codec.Utf16Le, Codec.Utf16Be, s_nativeUtf16, out start);
                    break;
                case Codec.Utf32:
                    unit = DetectBom(data, Codec.Utf32Le, Codec.Utf32Be, s_nativeUtf32, out start);
                    break;
                default:
                    unit = codec;
                    break;
            }

            Encoding source = GetEncoding(unit);
            if (!IsStrict(errors))
            {
                return DecodeWithErrors(self, data, start, codec, unit, source, errors);
            }
            try
            {
                return source.GetString(data, start, data.Length - start);
            }
            catch (DecoderFallbackException)
            {
                return DecodeWithErrors(self, data, start, codec, unit, source, errors);
            }
        }

        private static byte[] GetBytes(Encoding target, byte[]? bom, string s)
        {
            if (bom == null)
            {
                return target.GetBytes(s);
            }
            var result = new byte[bom.Length + target.GetByteCount(s)];
            Buffer.BlockCopy(bom, 0, result, 0, bom.Length);
            target.GetBytes(s, 0, s.Length, result, bom.Length);
            return result;
        }

        // Python's encoders: UTF-8, ASCII and Latin-1 report the whole run of unencodable characters,
        // UTF-16 and UTF-32 one character. Handlers replace character by character.
        private static Bytes EncodeWithErrors(string s, Codec codec, Codec unit, Encoding target, byte[]? bom, string errors)
        {
            bool runs = !IsWide(unit);
            StringBuilder? kept = null;
            Handler? handler = null;
            int segment = 0;
            int i = 0;
            while (i < s.Length)
            {
                if (IsEncodable(unit, s, i))
                {
                    i += Width(s, i);
                    continue;
                }

                int end = i + Width(s, i);
                while (runs && end < s.Length && !IsEncodable(unit, s, end))
                {
                    end += Width(s, end);
                }

                // Python resolves the handler only when an error occurs ('x'.encode('ascii', 'bogus') is b'x').
                handler ??= ResolveHandler(errors, decoding: false);
                if (handler == Handler.Strict)
                {
                    throw new UnicodeEncodeError(EncodeName(codec), s, i, end, EncodeReason(unit));
                }

                kept ??= new StringBuilder(s.Length + 16);
                kept.Append(s, segment, i - segment);
                for (int k = i; k < end; k += Width(s, k))
                {
                    AppendEncodeReplacement(kept, handler.Value, CodePoint(s, k));
                }
                i = end;
                segment = end;
            }
            if (kept == null)
            {
                return Bytes.Wrap(GetBytes(target, bom, s));
            }
            kept.Append(s, segment, s.Length - segment);
            return Bytes.Wrap(GetBytes(target, bom, kept.ToString()));
        }

        private static void AppendEncodeReplacement(StringBuilder text, Handler handler, int codePoint)
        {
            switch (handler)
            {
                case Handler.Replace:
                    text.Append('?');
                    break;
                case Handler.BackslashReplace:
                    if (codePoint < 0x100)
                    {
                        text.Append("\\x").Append(codePoint.ToString("x2", CultureInfo.InvariantCulture));
                    }
                    else if (codePoint < 0x10000)
                    {
                        text.Append("\\u").Append(codePoint.ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        text.Append("\\U").Append(codePoint.ToString("x8", CultureInfo.InvariantCulture));
                    }
                    break;
                case Handler.XmlCharRefReplace:
                    text.Append("&#").Append(codePoint.ToString(CultureInfo.InvariantCulture)).Append(';');
                    break;
            }
        }

        // Walks the bytes as CPython's decoder does: strict raises on the first error, replace writes one
        // U+FFFD per error, ignore drops it, backslashreplace writes each byte as \xNN. Positions count from
        // the start of the error's object: the whole input, except utf-8-sig, whose object is the input
        // after the BOM.
        private static string DecodeWithErrors(Bytes self, byte[] data, int start, Codec codec, Codec unit, Encoding source, string errors)
        {
            int origin = codec == Codec.Utf8Sig ? start : 0;
            StringBuilder? text = null;
            Handler? handler = null;
            int segment = start;
            int i = start;
            while (i < data.Length)
            {
                int valid = ValidLength(unit, data, i, out int end, out string reason);
                if (valid > 0)
                {
                    i += valid;
                    continue;
                }

                handler ??= ResolveHandler(errors, decoding: true);
                if (handler == Handler.Strict)
                {
                    Bytes obj = origin == 0 ? self : Bytes.Wrap(Slice(data, origin));
                    throw new UnicodeDecodeError(DecodeName(unit), obj, i - origin, end - origin, reason);
                }

                text ??= new StringBuilder(data.Length);
                text.Append(source.GetString(data, segment, i - segment));
                if (handler == Handler.Replace)
                {
                    text.Append('�');
                }
                else if (handler == Handler.BackslashReplace)
                {
                    for (int k = i; k < end; k++)
                    {
                        text.Append("\\x").Append(data[k].ToString("x2", CultureInfo.InvariantCulture));
                    }
                }
                i = end;
                segment = end;
            }
            if (text == null)
            {
                return source.GetString(data, start, data.Length - start);
            }
            text.Append(source.GetString(data, segment, data.Length - segment));
            return text.ToString();
        }

        // The byte length of the valid sequence at data[i], or 0 when an error starts there; then end is
        // where it stops (python's exclusive end) and reason is python's wording.
        private static int ValidLength(Codec unit, byte[] data, int i, out int end, out string reason)
        {
            end = i;
            reason = "";
            switch (unit)
            {
                case Codec.Utf8:
                    return Utf8Valid(data, i, out end, out reason);
                case Codec.Ascii:
                    if (data[i] < 0x80)
                    {
                        return 1;
                    }
                    end = i + 1;
                    reason = "ordinal not in range(128)";
                    return 0;
                case Codec.Latin1:
                    return 1;
                case Codec.Utf16Le:
                case Codec.Utf16Be:
                    return Utf16Valid(data, i, unit == Codec.Utf16Be, out end, out reason);
                default:
                    return Utf32Valid(data, i, unit == Codec.Utf32Be, out end, out reason);
            }
        }

        // CPython's UTF-8 decoder: an error is the maximal subpart of an ill-formed sequence.
        private static int Utf8Valid(byte[] data, int i, out int end, out string reason)
        {
            end = i;
            reason = "";
            byte lead = data[i];
            if (lead < 0x80)
            {
                return 1;
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
                end = i + 1;
                reason = "invalid start byte";
                return 0;
            }

            for (int k = 1; k <= continuations; k++)
            {
                int at = i + k;
                if (at >= data.Length)
                {
                    end = data.Length;
                    reason = "unexpected end of data";
                    return 0;
                }
                byte low = k == 1 ? secondLow : (byte)0x80;
                byte high = k == 1 ? secondHigh : (byte)0xBF;
                if (data[at] < low || data[at] > high)
                {
                    end = at;
                    reason = "invalid continuation byte";
                    return 0;
                }
            }
            return continuations + 1;
        }

        // CPython's UTF-16 decoder (PyUnicode_DecodeUTF16Stateful).
        private static int Utf16Valid(byte[] data, int i, bool bigEndian, out int end, out string reason)
        {
            end = i;
            reason = "";
            int remaining = data.Length - i;
            if (remaining < 2)
            {
                end = data.Length;
                reason = "truncated data";
                return 0;
            }
            int first = Read16(data, i, bigEndian);
            if (first < 0xD800 || first > 0xDFFF)
            {
                return 2;
            }
            if (first >= 0xDC00)
            {
                end = i + 2;
                reason = "illegal encoding";
                return 0;
            }
            if (remaining < 4)
            {
                end = data.Length;
                reason = "unexpected end of data";
                return 0;
            }
            int second = Read16(data, i + 2, bigEndian);
            if (second >= 0xDC00 && second <= 0xDFFF)
            {
                return 4;
            }
            end = i + 2;
            reason = "illegal UTF-16 surrogate";
            return 0;
        }

        // CPython's UTF-32 decoder (PyUnicode_DecodeUTF32Stateful).
        private static int Utf32Valid(byte[] data, int i, bool bigEndian, out int end, out string reason)
        {
            reason = "";
            if (data.Length - i < 4)
            {
                end = data.Length;
                reason = "truncated data";
                return 0;
            }
            end = i + 4;
            uint cp = bigEndian
                ? (uint)(data[i] << 24 | data[i + 1] << 16 | data[i + 2] << 8 | data[i + 3])
                : (uint)(data[i + 3] << 24 | data[i + 2] << 16 | data[i + 1] << 8 | data[i]);
            if (cp > 0x10FFFF)
            {
                reason = "code point not in range(0x110000)";
                return 0;
            }
            if (cp >= 0xD800 && cp <= 0xDFFF)
            {
                reason = "code point in surrogate code point range(0xd800, 0xe000)";
                return 0;
            }
            return 4;
        }

        private static int Read16(byte[] data, int i, bool bigEndian)
        {
            return bigEndian ? data[i] << 8 | data[i + 1] : data[i + 1] << 8 | data[i];
        }

        // Python's 'utf-16'/'utf-32' decoders: a leading BOM picks the byte order and is consumed;
        // without one the machine's order is used.
        private static Codec DetectBom(byte[] data, Codec little, Codec big, Codec native, out int start)
        {
            if (StartsWith(data, BomOf(little)))
            {
                start = BomOf(little).Length;
                return little;
            }
            if (StartsWith(data, BomOf(big)))
            {
                start = BomOf(big).Length;
                return big;
            }
            start = 0;
            return native;
        }

        private static bool StartsWith(byte[] data, byte[] prefix)
        {
            if (data.Length < prefix.Length)
            {
                return false;
            }
            for (int k = 0; k < prefix.Length; k++)
            {
                if (data[k] != prefix[k])
                {
                    return false;
                }
            }
            return true;
        }

        private static byte[] Slice(byte[] data, int start)
        {
            var result = new byte[data.Length - start];
            Buffer.BlockCopy(data, start, result, 0, result.Length);
            return result;
        }

        // A surrogate pair is one character (python's one code point); a lone surrogate is one.
        private static int Width(string s, int i)
        {
            return i + 1 < s.Length && char.IsSurrogatePair(s[i], s[i + 1]) ? 2 : 1;
        }

        private static int CodePoint(string s, int i)
        {
            return Width(s, i) == 2 ? char.ConvertToUtf32(s[i], s[i + 1]) : s[i];
        }

        private static bool IsEncodable(Codec unit, string s, int i)
        {
            char c = s[i];
            switch (unit)
            {
                case Codec.Ascii:
                    return c < 0x80;
                case Codec.Latin1:
                    return c < 0x100;
                default:
                    return !char.IsSurrogate(c) || Width(s, i) == 2;
            }
        }

        private static bool IsWide(Codec unit)
        {
            return unit == Codec.Utf16Le || unit == Codec.Utf16Be || unit == Codec.Utf32Le || unit == Codec.Utf32Be;
        }

        private static string EncodeReason(Codec unit)
        {
            switch (unit)
            {
                case Codec.Ascii:
                    return "ordinal not in range(128)";
                case Codec.Latin1:
                    return "ordinal not in range(256)";
                default:
                    return "surrogates not allowed";
            }
        }

        /// <summary>The fixed-order codec that does the byte work for <paramref name="codec"/>.</summary>
        private static Codec ByteOrder(Codec codec)
        {
            switch (codec)
            {
                case Codec.Utf8Sig:
                    return Codec.Utf8;
                case Codec.Utf16:
                    return s_nativeUtf16;
                case Codec.Utf32:
                    return s_nativeUtf32;
                default:
                    return codec;
            }
        }

        private static byte[]? EncodeBom(Codec codec)
        {
            switch (codec)
            {
                case Codec.Utf8Sig:
                    return s_bomUtf8;
                case Codec.Utf16:
                    return BomOf(s_nativeUtf16);
                case Codec.Utf32:
                    return BomOf(s_nativeUtf32);
                default:
                    return null;
            }
        }

        private static byte[] BomOf(Codec unit)
        {
            switch (unit)
            {
                case Codec.Utf16Le:
                    return s_bomUtf16Le;
                case Codec.Utf16Be:
                    return s_bomUtf16Be;
                case Codec.Utf32Le:
                    return s_bomUtf32Le;
                case Codec.Utf32Be:
                    return s_bomUtf32Be;
                default:
                    return s_bomUtf8;
            }
        }

        private static Encoding GetEncoding(Codec unit)
        {
            switch (unit)
            {
                case Codec.Ascii:
                    return s_ascii;
                case Codec.Latin1:
                    return s_latin1;
                case Codec.Utf16Le:
                    return s_utf16Le;
                case Codec.Utf16Be:
                    return s_utf16Be;
                case Codec.Utf32Le:
                    return s_utf32Le;
                case Codec.Utf32Be:
                    return s_utf32Be;
                default:
                    return s_utf8;
            }
        }

        // The name python's encoder errors carry: 'utf-16'/'utf-32' keep their own name, utf-8-sig is 'utf-8'.
        private static string EncodeName(Codec codec)
        {
            switch (codec)
            {
                case Codec.Utf16:
                    return "utf-16";
                case Codec.Utf32:
                    return "utf-32";
                default:
                    return DecodeName(ByteOrder(codec));
            }
        }

        // The name python's decoder errors carry: the byte order actually decoded.
        private static string DecodeName(Codec unit)
        {
            switch (unit)
            {
                case Codec.Ascii:
                    return "ascii";
                case Codec.Latin1:
                    return "latin-1";
                case Codec.Utf16Le:
                    return "utf-16-le";
                case Codec.Utf16Be:
                    return "utf-16-be";
                case Codec.Utf32Le:
                    return "utf-32-le";
                case Codec.Utf32Be:
                    return "utf-32-be";
                default:
                    return "utf-8";
            }
        }

        /// <summary>
        /// Python's codec lookup (<c>encodings.search_function</c>): the name normalised as
        /// <c>encodings.normalize_encoding</c> does, looked up in the alias table (also with '.' read as
        /// '_'), then as a module name, which may not contain '.'.
        /// </summary>
        private static Codec Resolve(string encoding)
        {
            if (s_spellings.TryGetValue(encoding, out Codec codec))
            {
                return codec;
            }
            if (encoding.IndexOf('\0') >= 0)
            {
                // Python checks the name is a C string before any lookup.
                throw new ValueError("embedded null character");
            }

            string name = Normalize(encoding);
            if (s_aliases.TryGetValue(name, out codec))
            {
                return codec;
            }
            if (name.IndexOf('.') >= 0)
            {
                if (s_aliases.TryGetValue(name.Replace('.', '_'), out codec))
                {
                    return codec;
                }
            }
            else if (s_modules.TryGetValue(name, out codec))
            {
                return codec;
            }
            throw new LookupError("unknown encoding: " + encoding);
        }

        // encodings.normalize_encoding (3.15) after the registry lowercases the name: ASCII letters, digits and
        // '.' are kept; every run of other characters, every non-ASCII character included, becomes one '_'
        // between kept characters.
        private static string Normalize(string encoding)
        {
            var name = new StringBuilder(encoding.Length);
            bool separator = false;
            for (int i = 0; i < encoding.Length; i++)
            {
                char c = encoding[i];
                bool kept = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.';
                if (c >= 'A' && c <= 'Z')
                {
                    c = (char)(c + ('a' - 'A'));
                    kept = true;
                }
                if (!kept)
                {
                    separator = true;
                    continue;
                }
                if (separator && name.Length > 0)
                {
                    name.Append('_');
                }
                name.Append(c);
                separator = false;
            }
            return name.ToString();
        }

        private static bool IsStrict(string errors)
        {
            return string.Equals(errors, "strict", StringComparison.Ordinal);
        }

        private static Handler ResolveHandler(string errors, bool decoding)
        {
            switch (errors)
            {
                case "strict":
                    return Handler.Strict;
                case "replace":
                    return Handler.Replace;
                case "ignore":
                    return Handler.Ignore;
                case "backslashreplace":
                    return Handler.BackslashReplace;
                case "xmlcharrefreplace":
                case "namereplace":
                    if (decoding)
                    {
                        // Python's encode-only handlers refuse a decode error.
                        throw new TypeError("don't know how to handle UnicodeDecodeError in error callback");
                    }
                    if (errors == "xmlcharrefreplace")
                    {
                        return Handler.XmlCharRefReplace;
                    }
                    return NotSupportedYet(errors);
                case "surrogateescape":
                case "surrogatepass":
                    return NotSupportedYet(errors);
            }
            throw new LookupError("unknown error handler name '" + errors + "'");
        }

        // A handler python has and Sharpy lacks: the name is right, the implementation is missing.
        private static Handler NotSupportedYet(string errors)
        {
            throw new LookupError("error handler '" + errors + "' is not supported yet (#2495)");
        }
    }
}
