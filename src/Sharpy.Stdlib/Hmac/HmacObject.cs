using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Represents an HMAC object that accumulates data and computes keyed-hash message authentication codes.
    /// Mirrors Python's hmac.HMAC object API.
    /// </summary>
    [SharpyModuleType("hmac")]
    public sealed class HmacObject
    {
        private readonly string _algorithmName;
        private readonly byte[] _key;
        private readonly System.Collections.Generic.List<byte> _data;

        /// <summary>
        /// The size of the resulting HMAC digest in bytes.
        /// </summary>
        public int DigestSize { get; }

        /// <summary>
        /// The canonical name of this hashing algorithm (e.g., "hmac-sha256").
        /// </summary>
        public string Name => "hmac-" + _algorithmName;

        /// <summary>Create a new HMAC object for the specified algorithm and key.</summary>
        /// <param name="key">The secret key as a byte array.</param>
        /// <param name="algorithmName">The hash algorithm name (e.g., "md5", "sha256").</param>
        /// <param name="digestSize">The size of the resulting HMAC digest in bytes.</param>
        internal HmacObject(byte[] key, string algorithmName, int digestSize)
        {
            _key = key;
            _algorithmName = algorithmName;
            DigestSize = digestSize;
            _data = new System.Collections.Generic.List<byte>();
        }

        /// <summary>Create a new HMAC object for the specified algorithm and key.</summary>
        /// <param name="key">The secret key as a string (UTF-8 encoded).</param>
        /// <param name="algorithmName">The hash algorithm name (e.g., "md5", "sha256").</param>
        /// <param name="digestSize">The size of the resulting HMAC digest in bytes.</param>
        internal HmacObject(string key, string algorithmName, int digestSize)
        {
            _key = Encoding.UTF8.GetBytes(key);
            _algorithmName = algorithmName;
            DigestSize = digestSize;
            _data = new System.Collections.Generic.List<byte>();
        }

        private HmacObject(byte[] key, string algorithmName, int digestSize, System.Collections.Generic.List<byte> existingData)
        {
            _key = key;
            _algorithmName = algorithmName;
            DigestSize = digestSize;
            _data = new System.Collections.Generic.List<byte>(existingData);
        }

        /// <summary>
        /// Append data to the HMAC object. The data is encoded as UTF-8.
        /// </summary>
        /// <param name="data">The string data to append.</param>
        public void Update(string data)
        {
            if (data == null)
            {
                throw new TypeError("a bytes-like object is required, not 'NoneType'");
            }

            byte[] bytes = Encoding.UTF8.GetBytes(data);
            _data.AddRange(bytes);
        }

        /// <summary>
        /// Return the hex-encoded string of the HMAC digest.
        /// </summary>
        /// <returns>A lowercase hex string of the computed HMAC.</returns>
        public string Hexdigest()
        {
            byte[] hash = ComputeHmac();
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
            {
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Return the raw HMAC digest as a list of integers (byte values 0-255).
        /// </summary>
        /// <returns>A <see cref="List{T}"/> of integer byte values.</returns>
        public List<int> Digest()
        {
            byte[] hash = ComputeHmac();
            var result = new System.Collections.Generic.List<int>(hash.Length);
            foreach (byte b in hash)
            {
                result.Add(b);
            }

            return new List<int>(result);
        }

        /// <summary>
        /// Return a copy of the HMAC object with the same accumulated data.
        /// </summary>
        /// <returns>A new <see cref="HmacObject"/> with the same state.</returns>
        public HmacObject Copy()
        {
            return new HmacObject(_key, _algorithmName, DigestSize, _data);
        }

        private byte[] ComputeHmac()
        {
            using (var hmac = CreateHmacAlgorithm())
            {
                return hmac.ComputeHash(_data.ToArray());
            }
        }

        private HMAC CreateHmacAlgorithm()
        {
            return _algorithmName switch
            {
                "md5" => new HMACMD5(_key),
                "sha1" => new HMACSHA1(_key),
                "sha256" => new HMACSHA256(_key),
                "sha384" => new HMACSHA384(_key),
                "sha512" => new HMACSHA512(_key),
                _ => throw new ValueError($"unsupported hash type '{_algorithmName}'")
            };
        }
    }
}
