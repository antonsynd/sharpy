using System.Security.Cryptography;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Secure hash and message digest algorithms, similar to Python's hashlib module.
    /// </summary>
    public static partial class Hashlib
    {
        /// <summary>
        /// Create an MD5 hash object, optionally initialized with data.
        /// </summary>
        /// <param name="data">Optional initial data to hash (encoded as UTF-8).</param>
        /// <returns>A new <see cref="HashObject"/> using the MD5 algorithm.</returns>
        public static HashObject Md5(string data = "")
        {
            var obj = new HashObject("md5", () => MD5.Create(), 16);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }

        /// <summary>
        /// Create a SHA-1 hash object, optionally initialized with data.
        /// </summary>
        /// <param name="data">Optional initial data to hash (encoded as UTF-8).</param>
        /// <returns>A new <see cref="HashObject"/> using the SHA-1 algorithm.</returns>
        public static HashObject Sha1(string data = "")
        {
            var obj = new HashObject("sha1", () => SHA1.Create(), 20);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }

        /// <summary>
        /// Create a SHA-256 hash object, optionally initialized with data.
        /// </summary>
        /// <param name="data">Optional initial data to hash (encoded as UTF-8).</param>
        /// <returns>A new <see cref="HashObject"/> using the SHA-256 algorithm.</returns>
        public static HashObject Sha256(string data = "")
        {
            var obj = new HashObject("sha256", () => SHA256.Create(), 32);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }

        /// <summary>
        /// Create a SHA-384 hash object, optionally initialized with data.
        /// </summary>
        /// <param name="data">Optional initial data to hash (encoded as UTF-8).</param>
        /// <returns>A new <see cref="HashObject"/> using the SHA-384 algorithm.</returns>
        public static HashObject Sha384(string data = "")
        {
            var obj = new HashObject("sha384", () => SHA384.Create(), 48);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }

        /// <summary>
        /// Create a SHA-512 hash object, optionally initialized with data.
        /// </summary>
        /// <param name="data">Optional initial data to hash (encoded as UTF-8).</param>
        /// <returns>A new <see cref="HashObject"/> using the SHA-512 algorithm.</returns>
        public static HashObject Sha512(string data = "")
        {
            var obj = new HashObject("sha512", () => SHA512.Create(), 64);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }
    }
}
