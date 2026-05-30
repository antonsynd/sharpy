using System;
using System.Security.Cryptography;
using System.Text;

namespace Sharpy
{
    public static partial class HmacModule
    {
        public static HmacObject New(Bytes key, Bytes? msg = null, string digestmod = "sha256")
        {
            if (string.IsNullOrEmpty(digestmod))
            {
                throw new TypeError("digestmod is required");
            }

            int digestSize = HmacObject.GetDigestSize(digestmod);
            var hmacObj = new HmacObject(key.ToArray(), digestmod, digestSize);
            if (msg != null)
            {
                hmacObj.Update(msg.Value);
            }

            return hmacObj;
        }

        public static HmacObject New(string key, string? msg = null, string digestmod = "sha256")
        {
            if (string.IsNullOrEmpty(digestmod))
            {
                throw new TypeError("digestmod is required");
            }

            int digestSize = HmacObject.GetDigestSize(digestmod);
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            var hmacObj = new HmacObject(keyBytes, digestmod, digestSize);
            if (msg != null && msg.Length > 0)
            {
                hmacObj.Update(msg);
            }

            return hmacObj;
        }

        public static Bytes Digest(Bytes key, Bytes msg, string digestmod)
        {
            if (string.IsNullOrEmpty(digestmod))
            {
                throw new TypeError("digestmod is required");
            }

            byte[] hash;
            using (var hmac = HmacObject.CreateHmacAlgorithm(digestmod, key.ToArray()))
            {
                hash = hmac.ComputeHash(msg.ToArray());
            }

            return new Bytes(hash);
        }

        public static Bytes Digest(string key, string msg, string digestmod)
        {
            return Digest(
                new Bytes(Encoding.UTF8.GetBytes(key)),
                new Bytes(Encoding.UTF8.GetBytes(msg)),
                digestmod);
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
