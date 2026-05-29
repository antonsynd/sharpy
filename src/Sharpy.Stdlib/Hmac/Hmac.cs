using System;
using System.Security.Cryptography;
using System.Text;

namespace Sharpy
{
    public static partial class HmacModule
    {
        /// <summary>
        /// Create a new HMAC object for incremental hashing.
        /// Mirrors Python's hmac.new(key, msg=None, digestmod='').
        /// </summary>
        /// <param name="key">The secret key as a string (UTF-8 encoded).</param>
        /// <param name="msg">Optional initial message to hash.</param>
        /// <param name="digestmod">The hash algorithm name (e.g., "sha256", "sha512").</param>
        /// <returns>A new <see cref="HmacObject"/>.</returns>
        public static HmacObject New(string key, string msg = "", string digestmod = "sha256")
        {
            if (string.IsNullOrEmpty(digestmod))
            {
                throw new TypeError("digestmod is required");
            }

            int digestSize = GetDigestSize(digestmod);
            var hmacObj = new HmacObject(key, digestmod, digestSize);
            if (msg.Length > 0)
            {
                hmacObj.Update(msg);
            }

            return hmacObj;
        }

        /// <summary>
        /// One-shot HMAC computation. Returns the hex-encoded HMAC digest.
        /// Mirrors Python's hmac.digest(key, msg, digest).
        /// </summary>
        /// <param name="key">The secret key as a string (UTF-8 encoded).</param>
        /// <param name="msg">The message to authenticate.</param>
        /// <param name="digestmod">The hash algorithm name (e.g., "sha256", "sha512").</param>
        /// <returns>A list of integers representing the raw HMAC digest bytes.</returns>
        public static List<int> Digest(string key, string msg, string digestmod)
        {
            if (string.IsNullOrEmpty(digestmod))
            {
                throw new TypeError("digestmod is required");
            }

            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] msgBytes = Encoding.UTF8.GetBytes(msg);
            byte[] hash;

            using (var hmac = CreateHmacAlgorithm(digestmod, keyBytes))
            {
                hash = hmac.ComputeHash(msgBytes);
            }

            var result = new System.Collections.Generic.List<int>(hash.Length);
            foreach (byte b in hash)
            {
                result.Add(b);
            }

            return new List<int>(result);
        }

        /// <summary>
        /// Constant-time comparison of two strings to prevent timing attacks.
        /// Mirrors Python's hmac.compare_digest(a, b).
        /// </summary>
        /// <param name="a">First value to compare.</param>
        /// <param name="b">Second value to compare.</param>
        /// <returns>True if the values are equal, false otherwise.</returns>
        public static bool CompareDigest(string a, string b)
        {
            byte[] aBytes = Encoding.UTF8.GetBytes(a);
            byte[] bBytes = Encoding.UTF8.GetBytes(b);

            return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
        }

        private static int GetDigestSize(string algorithmName)
        {
            return algorithmName switch
            {
                "md5" => 16,
                "sha1" => 20,
                "sha256" => 32,
                "sha384" => 48,
                "sha512" => 64,
                _ => throw new ValueError($"unsupported hash type '{algorithmName}'")
            };
        }

        private static HMAC CreateHmacAlgorithm(string algorithmName, byte[] key)
        {
            return algorithmName switch
            {
                "md5" => new HMACMD5(key),
                "sha1" => new HMACSHA1(key),
                "sha256" => new HMACSHA256(key),
                "sha384" => new HMACSHA384(key),
                "sha512" => new HMACSHA512(key),
                _ => throw new ValueError($"unsupported hash type '{algorithmName}'")
            };
        }
    }
}
