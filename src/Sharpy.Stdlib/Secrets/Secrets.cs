using System;
using System.Security.Cryptography;
using System.Text;

namespace Sharpy
{
    /// <summary>Provides cryptographically strong helpers similar to Python's secrets module.</summary>
    public static partial class SecretsModule
    {
        /// <summary>Return a random byte string containing nbytes bytes.</summary>
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

        /// <summary>Return a random text string with nbytes random bytes encoded as hex.</summary>
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

        /// <summary>Return a random URL-safe text string containing nbytes random bytes.</summary>
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

        /// <summary>Return a random int in the range [0, exclusiveUpperBound).</summary>
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

        /// <summary>Return a random element from a non-empty sequence.</summary>
        public static T Choice<T>(List<T> sequence)
        {
            int len = ((ISized)sequence).Count;
            if (len == 0)
            {
                throw new IndexError("cannot choose from an empty sequence");
            }

            return sequence[Randbelow(len)];
        }

        /// <summary>Compare two strings in constant time.</summary>
        public static bool CompareDigest(string a, string b)
        {
            byte[] aBytes = Encoding.UTF8.GetBytes(a);
            byte[] bBytes = Encoding.UTF8.GetBytes(b);
            return FixedTimeEquals(aBytes, bBytes);
        }

        /// <summary>Compare two byte sequences in constant time.</summary>
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
