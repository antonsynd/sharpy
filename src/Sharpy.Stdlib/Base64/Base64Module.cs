using System;
using System.Text;

namespace Sharpy
{
    public static partial class Base64Module
    {
        // ─── Base64 ──────────────────────────────────────────────────────────

        public static Bytes B64encode(Bytes s, Bytes? altchars = null)
        {
            byte[] data = s.ToArray();
            string encoded = Convert.ToBase64String(data);

            if (altchars != null)
            {
                byte[] alt = altchars.Value.ToArray();
                if (alt.Length != 2)
                {
                    throw new ValueError("altchars must be a bytes object of length 2");
                }

                encoded = encoded.Replace('+', (char)alt[0]).Replace('/', (char)alt[1]);
            }

            return new Bytes(Encoding.ASCII.GetBytes(encoded));
        }

        public static Bytes B64decode(Bytes s, Bytes? altchars = null, bool validate = false)
        {
            return B64decodeString(Encoding.ASCII.GetString(s.ToArray()), altchars, validate);
        }

        public static Bytes B64decode(string s, Bytes? altchars = null, bool validate = false)
        {
            return B64decodeString(s, altchars, validate);
        }

        private static Bytes B64decodeString(string encoded, Bytes? altchars, bool validate)
        {
            if (altchars != null)
            {
                byte[] alt = altchars.Value.ToArray();
                if (alt.Length != 2)
                {
                    throw new ValueError("altchars must be a bytes object of length 2");
                }

                encoded = encoded.Replace((char)alt[0], '+').Replace((char)alt[1], '/');
            }

            if (validate)
            {
                foreach (char c in encoded)
                {
                    if (!IsBase64Char(c) && c != '=' && c != '\n' && c != '\r')
                    {
                        throw new ValueError("Invalid character in input");
                    }
                }
            }
            else
            {
                var sb = new StringBuilder(encoded.Length);
                foreach (char c in encoded)
                {
                    if (IsBase64Char(c) || c == '=')
                    {
                        sb.Append(c);
                    }
                }

                encoded = sb.ToString();
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

        public static Bytes UrlsafeB64encode(Bytes s)
        {
            byte[] data = s.ToArray();
            string encoded = Convert.ToBase64String(data);
            encoded = encoded.Replace('+', '-').Replace('/', '_');
            return new Bytes(Encoding.ASCII.GetBytes(encoded));
        }

        public static Bytes UrlsafeB64decode(Bytes s)
        {
            return UrlsafeB64decodeString(Encoding.ASCII.GetString(s.ToArray()));
        }

        public static Bytes UrlsafeB64decode(string s)
        {
            return UrlsafeB64decodeString(s);
        }

        private static Bytes UrlsafeB64decodeString(string encoded)
        {
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

            while (sb.Length % 8 != 0)
            {
                sb.Append('=');
            }

            return new Bytes(Encoding.ASCII.GetBytes(sb.ToString()));
        }

        public static Bytes B32decode(Bytes s, bool casefold = false)
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
                int val = Base32CharToValue(c, casefold);
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

        private static int Base32CharToValue(char c, bool casefold)
        {
            if (c >= 'A' && c <= 'Z')
            {
                return c - 'A';
            }

            if (c >= 'a' && c <= 'z')
            {
                if (!casefold)
                {
                    return -1;
                }

                return c - 'a';
            }

            if (c >= '2' && c <= '7')
            {
                return c - '2' + 26;
            }

            return -1;
        }

        // ─── Base16 ──────────────────────────────────────────────────────────

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

        public static Bytes B16decode(Bytes s, bool casefold = false)
        {
            string hex = Encoding.ASCII.GetString(s.ToArray());
            if (hex.Length % 2 != 0)
            {
                throw new ValueError("Odd-length string");
            }

            var result = new byte[hex.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                int hi = HexCharToValue(hex[i * 2], casefold);
                int lo = HexCharToValue(hex[i * 2 + 1], casefold);
                if (hi < 0 || lo < 0)
                {
                    throw new ValueError("Non-hexadecimal digit found");
                }

                result[i] = (byte)((hi << 4) | lo);
            }

            return new Bytes(result);
        }

        private static int HexCharToValue(char c, bool casefold)
        {
            if (c >= '0' && c <= '9')
            {
                return c - '0';
            }

            if (c >= 'A' && c <= 'F')
            {
                return c - 'A' + 10;
            }

            if (c >= 'a' && c <= 'f')
            {
                if (!casefold)
                {
                    return -1;
                }

                return c - 'a' + 10;
            }

            return -1;
        }

        // ─── Base85 (RFC 1924) ───────────────────────────────────────────────

        private static readonly char[] B85Alphabet =
            "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz!#$%&()*+-;<=>?@^_`{|}~".ToCharArray();

        private static readonly int[] B85Lookup = BuildB85Lookup();

        private static int[] BuildB85Lookup()
        {
            var lookup = new int[128];
            for (int i = 0; i < 128; i++)
            {
                lookup[i] = -1;
            }

            for (int i = 0; i < B85Alphabet.Length; i++)
            {
                lookup[B85Alphabet[i]] = i;
            }

            return lookup;
        }

        public static Bytes B85encode(Bytes s)
        {
            byte[] data = s.ToArray();
            if (data.Length == 0)
            {
                return new Bytes(System.Array.Empty<byte>());
            }

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

            int resultLen = sb.Length - padding;
            return new Bytes(Encoding.ASCII.GetBytes(sb.ToString(0, resultLen)));
        }

        public static Bytes B85decode(Bytes s)
        {
            byte[] data = s.ToArray();
            if (data.Length == 0)
            {
                return new Bytes(System.Array.Empty<byte>());
            }

            int padding = (5 - (data.Length % 5)) % 5;
            byte[] padded = new byte[data.Length + padding];
            System.Array.Copy(data, padded, data.Length);
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

            int finalLen = result.Length - padding;
            var final_ = new byte[finalLen];
            System.Array.Copy(result, final_, finalLen);
            return new Bytes(final_);
        }

        private static int B85CharToValue(char c)
        {
            if (c < 0 || c >= 128)
            {
                return -1;
            }

            return B85Lookup[c];
        }

        // ─── Ascii85 (Adobe variant) ─────────────────────────────────────────

        public static Bytes A85encode(Bytes s)
        {
            byte[] data = s.ToArray();
            if (data.Length == 0)
            {
                return new Bytes(System.Array.Empty<byte>());
            }

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

            int resultLen = sb.Length - padding;
            return new Bytes(Encoding.ASCII.GetBytes(sb.ToString(0, resultLen)));
        }

        public static Bytes A85decode(Bytes s)
        {
            byte[] data = s.ToArray();
            if (data.Length == 0)
            {
                return new Bytes(System.Array.Empty<byte>());
            }

            var expanded = new System.Collections.Generic.List<byte>(data.Length * 2);
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] == (byte)'z')
                {
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
            int a85padding = (5 - (chars.Length % 5)) % 5;
            byte[] paddedChars = new byte[chars.Length + a85padding];
            System.Array.Copy(chars, paddedChars, chars.Length);
            for (int i = chars.Length; i < paddedChars.Length; i++)
            {
                paddedChars[i] = (byte)'u';
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

            int finalLen = result.Count - a85padding;
            var final_ = new byte[finalLen];
            for (int i = 0; i < finalLen; i++)
            {
                final_[i] = result[i];
            }

            return new Bytes(final_);
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        private static bool IsBase64Char(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                   (c >= '0' && c <= '9') || c == '+' || c == '/';
        }
    }
}
