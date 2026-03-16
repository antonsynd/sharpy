using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Represents a hash object that accumulates data and computes cryptographic hashes.
    /// Mirrors Python's hashlib hash object API.
    /// </summary>
    [SharpyModuleType("hashlib")]
    public sealed class HashObject
    {
        private readonly string _algorithmName;
        private readonly Func<HashAlgorithm> _algorithmFactory;
        private readonly System.Collections.Generic.List<byte> _data;

        /// <summary>
        /// The size of the resulting hash in bytes.
        /// </summary>
        public int DigestSize { get; }

        /// <summary>
        /// The canonical name of this hashing algorithm (e.g., "sha256").
        /// </summary>
        public string Name => _algorithmName;

        internal HashObject(string algorithmName, Func<HashAlgorithm> algorithmFactory, int digestSize)
        {
            _algorithmName = algorithmName;
            _algorithmFactory = algorithmFactory;
            DigestSize = digestSize;
            _data = new System.Collections.Generic.List<byte>();
        }

        internal HashObject(string algorithmName, Func<HashAlgorithm> algorithmFactory, int digestSize, System.Collections.Generic.List<byte> existingData)
        {
            _algorithmName = algorithmName;
            _algorithmFactory = algorithmFactory;
            DigestSize = digestSize;
            _data = new System.Collections.Generic.List<byte>(existingData);
        }

        /// <summary>
        /// Append data to the hash object. The data is encoded as UTF-8.
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
        /// Return the hex-encoded string of the hash digest.
        /// </summary>
        /// <returns>A lowercase hex string of the computed hash.</returns>
        public string Hexdigest()
        {
            byte[] hash = ComputeHash();
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
            {
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Return the raw hash digest as a list of integers (byte values 0-255).
        /// </summary>
        /// <returns>A <see cref="List{T}"/> of integer byte values.</returns>
        public List<int> Digest()
        {
            byte[] hash = ComputeHash();
            var result = new System.Collections.Generic.List<int>(hash.Length);
            foreach (byte b in hash)
            {
                result.Add(b);
            }

            return new List<int>(result);
        }

        /// <summary>
        /// Return a copy of the hash object with the same accumulated data.
        /// </summary>
        /// <returns>A new <see cref="HashObject"/> with the same state.</returns>
        public HashObject Copy()
        {
            return new HashObject(_algorithmName, _algorithmFactory, DigestSize, _data);
        }

        private byte[] ComputeHash()
        {
            using (var algorithm = _algorithmFactory())
            {
                return algorithm.ComputeHash(_data.ToArray());
            }
        }
    }
}
