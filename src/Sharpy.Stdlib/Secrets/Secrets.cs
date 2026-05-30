using System;
using System.Security.Cryptography;
using System.Text;

namespace Sharpy
{
    public static partial class SecretsModule
    {
        public static Bytes TokenBytes(int nbytes = 32)
        {
            if (nbytes < 0)
            {
                throw new ValueError("nbytes must be non-negative");
            }

            var buf = new byte[nbytes];
#if NET10_0_OR_GREATER
            RandomNumberGenerator.Fill(buf);
#else
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(buf);
            }
#endif
            return new Bytes(buf);
        }

        public static string TokenHex(int nbytes = 32)
        {
            if (nbytes < 0)
            {
                throw new ValueError("nbytes must be non-negative");
            }

            if (nbytes == 0)
            {
                return "";
            }

            var buf = new byte[nbytes];
#if NET10_0_OR_GREATER
            RandomNumberGenerator.Fill(buf);
#else
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(buf);
            }
#endif
            var sb = new StringBuilder(nbytes * 2);
            foreach (byte b in buf)
            {
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }

        public static string TokenUrlsafe(int nbytes = 32)
        {
            if (nbytes < 0)
            {
                throw new ValueError("nbytes must be non-negative");
            }

            if (nbytes == 0)
            {
                return "";
            }

            var buf = new byte[nbytes];
#if NET10_0_OR_GREATER
            RandomNumberGenerator.Fill(buf);
#else
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(buf);
            }
#endif
            return Convert.ToBase64String(buf)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        public static int Randbelow(int exclusiveUpperBound)
        {
            if (exclusiveUpperBound <= 0)
            {
                throw new ValueError("upper bound must be positive");
            }

#if NET10_0_OR_GREATER
            return RandomNumberGenerator.GetInt32(exclusiveUpperBound);
#else
            var buf = new byte[4];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(buf);
            }

            int val = BitConverter.ToInt32(buf, 0) & int.MaxValue;
            return val % exclusiveUpperBound;
#endif
        }

        public static T Choice<T>(List<T> sequence)
        {
            int len = ((ISized)sequence).Count;
            if (len == 0)
            {
                throw new IndexError("cannot choose from an empty sequence");
            }

            return sequence[Randbelow(len)];
        }

        public static bool CompareDigest(string a, string b)
        {
            byte[] aBytes = Encoding.UTF8.GetBytes(a);
            byte[] bBytes = Encoding.UTF8.GetBytes(b);
            return FixedTimeEquals(aBytes, bBytes);
        }

        public static bool CompareDigest(Bytes a, Bytes b)
        {
            return FixedTimeEquals(a.ToArray(), b.ToArray());
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
#if NET10_0_OR_GREATER
            return CryptographicOperations.FixedTimeEquals(a, b);
#else
            if (a.Length != b.Length)
            {
                return false;
            }

            int result = 0;
            for (int i = 0; i < a.Length; i++)
            {
                result |= a[i] ^ b[i];
            }

            return result == 0;
#endif
        }
    }
}
