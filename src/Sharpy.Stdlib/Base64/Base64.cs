using System;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Python-compatible base64 module.
    /// Provides base16, base32, base64, and base85 encoding/decoding functions.
    /// </summary>
    public static partial class Base64Module
    {
        // ─── Base64 ──────────────────────────────────────────────────────────

        /// <summary>Encode bytes using standard Base64.</summary>
        /// <param name="s">The bytes to encode.</param>
        /// <param name="altchars">Optional 2-byte replacement for '+' and '/'.</param>
        /// <returns>Base64-encoded bytes.</returns>
        public static Bytes B64encode(Bytes s, Bytes? altchars = null)
        {
            byte[] data = s.ToArray();
            string encoded = Convert.ToBase64String(data);

            if (altchars != null)
            {
                byte[] alt = altchars.Value.ToArray();
                if (alt.Length != 2)
                {
                    throw new ValueError("altchars must be a 2-byte string");
                }
                encoded = encoded.Replace('+', (char)alt[0]).Replace('/', (char)alt[1]);
            }

            return new Bytes(Encoding.ASCII.GetBytes(encoded));
        }

        /// <summary>Decode a Base64 encoded bytes object.</summary>
        /// <param name="s">The Base64-encoded bytes to decode.</param>
        /// <param name="altchars">Optional 2-byte replacement for '+' and '/'.</param>
        /// <param name="validate">If true, non-base64 characters before padding raise an error.</param>
        /// <returns>Decoded bytes.</returns>
        public static Bytes B64decode(Bytes s, Bytes? altchars = null, bool validate = false)
        {
            string encoded = Encoding.ASCII.GetString(s.ToArray());

            if (altchars != null)
            {
                byte[] alt = altchars.Value.ToArray();
                if (alt.Length != 2)
                {
                    throw new ValueError("altchars must be a 2-byte string");
                }
                encoded = encoded.Replace((char)alt[0], '+').Replace((char)alt[1], '/');
            }

            if (validate)
            {
                foreach (char c in encoded)
                {
                    if (!IsBase64Char(c) && c != '=')
                    {
                        throw new ValueError("Invalid base64-encoded string: number of data characters cannot be 1 more than a multiple of 4");
                    }
                }
            }

            try
            {
                byte[] decoded = Convert.FromBase64String(encoded);
                return new Bytes(decoded);
            }
            catch (FormatException ex)
            {
                throw new ValueError("Invalid base64-encoded string: " + ex.Message);
            }
        }

        // ─── URL-safe Base64 ─────────────────────────────────────────────────

        /// <summary>Encode bytes using URL-safe Base64 (uses '-' and '_' instead of '+' and '/').</summary>
        /// <param name="s">The bytes to encode.</param>
        /// <returns>URL-safe Base64-encoded bytes.</returns>
        public static Bytes UrlsafeB64encode(Bytes s)
        {
            byte[] data = s.ToArray();
            string encoded = Convert.ToBase64String(data);
            encoded = encoded.Replace('+', '-').Replace('/', '_');
            return new Bytes(Encoding.ASCII.GetBytes(encoded));
        }

        /// <summary>Decode a URL-safe Base64 encoded bytes object.</summary>
        /// <param name="s">The URL-safe Base64-encoded bytes to decode.</param>
        /// <returns>Decoded bytes.</returns>
        public static Bytes UrlsafeB64decode(Bytes s)
        {
            string encoded = Encoding.ASCII.GetString(s.ToArray());
            encoded = encoded.Replace('-', '+').Replace('_', '/');
            try
            {
                byte[] decoded = Convert.FromBase64String(encoded);
                return new Bytes(decoded);
            }
            catch (FormatException ex)
            {
                throw new ValueError("Invalid base64-encoded string: " + ex.Message);
            }
        }

        // ─── Base32 ──────────────────────────────────────────────────────────

        private static readonly char[] Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".ToCharArray();

        /// <summary>Encode bytes using Base32.</summary>
        /// <param name="s">The bytes to encode.</param>
        /// <returns>Base32-encoded bytes.</returns>
        public static Bytes B32encode(Bytes s)
        {
            byte[] data = s.ToArray();
            if (data.Length == 0)
            {
                return new Bytes(System.Array.Empty<byte>());
            }

            var sb = new StringBuilder((data.Length * 8 + 4) / 5);
            int buffer = 0;
            int bitsLeft = 0;

            for (int i = 0; i < data.Length; i++)
            {
                buffer = (buffer << 8) | data[i];
                bitsLeft += 8;
                while (bitsLeft >= 5)
                {
                    bitsLeft -= 5;
                    sb.Append(Base32Alphabet[(buffer >> bitsLeft) & 0x1F]);
                }
            }

            if (bitsLeft > 0)
            {
                sb.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 0x1F]);
            }

            // Pad to multiple of 8
            while (sb.Length % 8 != 0)
            {
                sb.Append('=');
            }

            return new Bytes(Encoding.ASCII.GetBytes(sb.ToString()));
        }

        /// <summary>Decode a Base32 encoded bytes object.</summary>
        /// <param name="s">The Base32-encoded bytes to decode.</param>
        /// <returns>Decoded bytes.</returns>
        public static Bytes B32decode(Bytes s)
        {
            string encoded = Encoding.ASCII.GetString(s.ToArray()).TrimEnd('=');
            if (encoded.Length == 0)
            {
                return new Bytes(System.Array.Empty<byte>());
            }

            var result = new byte[encoded.Length * 5 / 8];
            int buffer = 0;
            int bitsLeft = 0;
            int index = 0;

            foreach (char c in encoded)
            {
                int val = Base32CharToValue(c);
                if (val < 0)
                {
                    throw new ValueError("Non-base32 digit found");
                }
                buffer = (buffer << 5) | val;
                bitsLeft += 5;
                if (bitsLeft >= 8)
                {
                    bitsLeft -= 8;
                    result[index++] = (byte)((buffer >> bitsLeft) & 0xFF);
                }
            }

            if (index != result.Length)
            {
                var trimmed = new byte[index];
                System.Array.Copy(result, trimmed, index);
                return new Bytes(trimmed);
            }

            return new Bytes(result);
        }

        private static int Base32CharToValue(char c)
        {
            if (c >= 'A' && c <= 'Z') return c - 'A';
            if (c >= 'a' && c <= 'z') return c - 'a';
            if (c >= '2' && c <= '7') return c - '2' + 26;
            return -1;
        }

        // ─── Base16 ──────────────────────────────────────────────────────────

        /// <summary>Encode bytes using Base16 (hex, uppercase).</summary>
        /// <param name="s">The bytes to encode.</param>
        /// <returns>Base16-encoded bytes (uppercase hex).</returns>
        public static Bytes B16encode(Bytes s)
        {
            byte[] data = s.ToArray();
            var sb = new StringBuilder(data.Length * 2);
            for (int i = 0; i < data.Length; i++)
            {
                sb.Append(data[i].ToString("X2"));
            }
            return new Bytes(Encoding.ASCII.GetBytes(sb.ToString()));
        }

        /// <summary>Decode a Base16 (hex) encoded bytes object.</summary>
        /// <param name="s">The Base16-encoded bytes to decode.</param>
        /// <returns>Decoded bytes.</returns>
        public static Bytes B16decode(Bytes s)
        {
            string hex = Encoding.ASCII.GetString(s.ToArray());
            if (hex.Length % 2 != 0)
            {
                throw new ValueError("Odd-length string");
            }

            var result = new byte[hex.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                int hi = HexCharToValue(hex[i * 2]);
                int lo = HexCharToValue(hex[i * 2 + 1]);
                if (hi < 0 || lo < 0)
                {
                    throw new ValueError("Non-hexadecimal digit found");
                }
                result[i] = (byte)((hi << 4) | lo);
            }
            return new Bytes(result);
        }

        private static int HexCharToValue(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            return -1;
        }

        // ─── Base85 (RFC 1924 / btoa variant) ────────────────────────────────

        private static readonly char[] B85Alphabet =
            "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz!#$%&()*+-;<=>?@^_`{|}~".ToCharArray();

        /// <summary>Encode bytes using Base85 (RFC 1924 variant, same as Python's b85encode).</summary>
        /// <param name="s">The bytes to encode.</param>
        /// <returns>Base85-encoded bytes.</returns>
        public static Bytes B85encode(Bytes s)
        {
            byte[] data = s.ToArray();
            if (data.Length == 0)
            {
                return new Bytes(System.Array.Empty<byte>());
            }

            // Pad to multiple of 4
            int padding = (4 - (data.Length % 4)) % 4;
            byte[] padded = new byte[data.Length + padding];
            System.Array.Copy(data, padded, data.Length);

            var sb = new StringBuilder(padded.Length * 5 / 4);

            for (int i = 0; i < padded.Length; i += 4)
            {
                uint acc = ((uint)padded[i] << 24) | ((uint)padded[i + 1] << 16) |
                           ((uint)padded[i + 2] << 8) | padded[i + 3];

                char[] chunk = new char[5];
                for (int j = 4; j >= 0; j--)
                {
                    chunk[j] = B85Alphabet[(int)(acc % 85)];
                    acc /= 85;
                }
                sb.Append(chunk);
            }

            // Remove padding characters
            int resultLen = sb.Length - padding;
            return new Bytes(Encoding.ASCII.GetBytes(sb.ToString(0, resultLen)));
        }

        /// <summary>Decode Base85-encoded bytes (RFC 1924 variant).</summary>
        /// <param name="s">The Base85-encoded bytes to decode.</param>
        /// <returns>Decoded bytes.</returns>
        public static Bytes B85decode(Bytes s)
        {
            byte[] data = s.ToArray();
            if (data.Length == 0)
            {
                return new Bytes(System.Array.Empty<byte>());
            }

            // Pad to multiple of 5
            int padding = (5 - (data.Length % 5)) % 5;
            byte[] padded = new byte[data.Length + padding];
            System.Array.Copy(data, padded, data.Length);
            // Pad with last char of alphabet ('~')
            for (int i = data.Length; i < padded.Length; i++)
            {
                padded[i] = (byte)'~';
            }

            var result = new byte[padded.Length * 4 / 5];
            int outIdx = 0;

            for (int i = 0; i < padded.Length; i += 5)
            {
                uint acc = 0;
                for (int j = 0; j < 5; j++)
                {
                    int val = B85CharToValue((char)padded[i + j]);
                    if (val < 0)
                    {
                        throw new ValueError("Invalid base85 character");
                    }
                    acc = acc * 85 + (uint)val;
                }
                result[outIdx++] = (byte)(acc >> 24);
                result[outIdx++] = (byte)(acc >> 16);
                result[outIdx++] = (byte)(acc >> 8);
                result[outIdx++] = (byte)(acc);
            }

            // Remove padding
            int finalLen = result.Length - padding;
            var final = new byte[finalLen];
            System.Array.Copy(result, final, finalLen);
            return new Bytes(final);
        }

        private static int B85CharToValue(char c)
        {
            for (int i = 0; i < B85Alphabet.Length; i++)
            {
                if (B85Alphabet[i] == c) return i;
            }
            return -1;
        }

        // ─── Ascii85 (Adobe variant) ────────────────────────────────────────

        /// <summary>Encode bytes using Ascii85 (Adobe variant).</summary>
        /// <param name="s">The bytes to encode.</param>
        /// <returns>Ascii85-encoded bytes.</returns>
        public static Bytes A85encode(Bytes s)
        {
            byte[] data = s.ToArray();
            if (data.Length == 0)
            {
                return new Bytes(System.Array.Empty<byte>());
            }

            // Pad to multiple of 4
            int padding = (4 - (data.Length % 4)) % 4;
            byte[] padded = new byte[data.Length + padding];
            System.Array.Copy(data, padded, data.Length);

            var sb = new StringBuilder(padded.Length * 5 / 4);

            for (int i = 0; i < padded.Length; i += 4)
            {
                uint acc = ((uint)padded[i] << 24) | ((uint)padded[i + 1] << 16) |
                           ((uint)padded[i + 2] << 8) | padded[i + 3];

                if (acc == 0 && i + 4 <= data.Length)
                {
                    sb.Append('z');
                }
                else
                {
                    char[] chunk = new char[5];
                    for (int j = 4; j >= 0; j--)
                    {
                        chunk[j] = (char)(acc % 85 + 33);
                        acc /= 85;
                    }
                    sb.Append(chunk);
                }
            }

            // Remove padding characters
            int resultLen = sb.Length - padding;
            return new Bytes(Encoding.ASCII.GetBytes(sb.ToString(0, resultLen)));
        }

        /// <summary>Decode Ascii85-encoded bytes (Adobe variant).</summary>
        /// <param name="s">The Ascii85-encoded bytes to decode.</param>
        /// <returns>Decoded bytes.</returns>
        public static Bytes A85decode(Bytes s)
        {
            byte[] data = s.ToArray();
            if (data.Length == 0)
            {
                return new Bytes(System.Array.Empty<byte>());
            }

            // Expand 'z' shorthand and collect decoded groups
            var expanded = new System.Collections.Generic.List<byte>(data.Length * 2);
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] == (byte)'z')
                {
                    // 'z' represents four zero bytes
                    expanded.Add((byte)'!');
                    expanded.Add((byte)'!');
                    expanded.Add((byte)'!');
                    expanded.Add((byte)'!');
                    expanded.Add((byte)'!');
                }
                else
                {
                    expanded.Add(data[i]);
                }
            }

            byte[] chars = expanded.ToArray();
            // Pad to multiple of 5
            int padding = (5 - (chars.Length % 5)) % 5;
            byte[] paddedChars = new byte[chars.Length + padding];
            System.Array.Copy(chars, paddedChars, chars.Length);
            for (int i = chars.Length; i < paddedChars.Length; i++)
            {
                paddedChars[i] = (byte)'u'; // 'u' = 117 = 84 + 33 (max Ascii85 char)
            }

            var result = new System.Collections.Generic.List<byte>(paddedChars.Length * 4 / 5);

            for (int i = 0; i < paddedChars.Length; i += 5)
            {
                uint acc = 0;
                for (int j = 0; j < 5; j++)
                {
                    int val = paddedChars[i + j] - 33;
                    if (val < 0 || val > 84)
                    {
                        throw new ValueError("Invalid Ascii85 character");
                    }
                    acc = acc * 85 + (uint)val;
                }
                result.Add((byte)(acc >> 24));
                result.Add((byte)(acc >> 16));
                result.Add((byte)(acc >> 8));
                result.Add((byte)(acc));
            }

            // Remove padding bytes
            int finalLen = result.Count - padding;
            var final = new byte[finalLen];
            for (int i = 0; i < finalLen; i++)
            {
                final[i] = result[i];
            }
            return new Bytes(final);
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        private static bool IsBase64Char(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                   (c >= '0' && c <= '9') || c == '+' || c == '/';
        }
    }
}
