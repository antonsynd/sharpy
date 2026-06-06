// Generated from src/Sharpy.Stdlib/spy/hashlib_module.spy — do not edit directly.
// To regenerate: sharpyc emit csharp src/Sharpy.Stdlib/spy/hashlib_module.spy -t library -n Sharpy
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace Sharpy
{
    /// <summary>
    /// Secure hash and message digest algorithms (MD5, SHA-1, SHA-2 family).
    /// </summary>
    public static partial class HashlibModule
    {
        /// <summary>
        /// Represents a hash object that accumulates data and computes cryptographic hashes. Mirrors Python's hashlib hash object API.
        /// </summary>
        public class HashObject
        {
            protected Sharpy.List<byte> _Data;
            protected global::System.Security.Cryptography.HashAlgorithm _CreateAlgorithm()
            {
                switch (this.Name)
                {
                    case "md5":
                        return global::System.Security.Cryptography.MD5.Create();
                    case "sha1":
                        return global::System.Security.Cryptography.SHA1.Create();
                    case "sha256":
                        return global::System.Security.Cryptography.SHA256.Create();
                    case "sha384":
                        return global::System.Security.Cryptography.SHA384.Create();
                    case "sha512":
                        return global::System.Security.Cryptography.SHA512.Create();
                    default:
                        throw new global::Sharpy.ValueError("unsupported hash type '" + this.Name + "'");
                }
            }

            protected Sharpy.List<byte> _ComputeHash()
            {
                using (var algorithm = this._CreateAlgorithm())
                {
                    return algorithm.ComputeHash(this._Data.ToArray());
                }
            }

            /// <summary>
            /// Append data to the hash object. The data is encoded as UTF-8.
            /// </summary>
            public void Update(string data)
            {
                Sharpy.List<byte> newBytes = global::System.Text.Encoding.UTF8.GetBytes(data);
                foreach (var __loopVar_0 in newBytes)
                {
                    var b = __loopVar_0;
                    this._Data.Append(b);
                }
            }

            /// <summary>
            /// Return the hex-encoded string of the hash digest.
            /// </summary>
            public string Hexdigest()
            {
                Sharpy.List<byte> hashBytes = this._ComputeHash();
                global::System.Text.StringBuilder sb = new global::System.Text.StringBuilder();
                foreach (var __loopVar_1 in hashBytes)
                {
                    var b = __loopVar_1;
                    sb.Append(b.ToString("x2"));
                }

                return sb.ToString();
            }

            /// <summary>
            /// Return the raw hash digest as a list of integers (byte values 0-255).
            /// </summary>
            public Sharpy.List<int> Digest()
            {
                Sharpy.List<byte> hashBytes = this._ComputeHash();
                Sharpy.List<int> result = new Sharpy.List<int>()
                {
                };
                foreach (var __loopVar_2 in hashBytes)
                {
                    var b = __loopVar_2;
                    result.Append(global::Sharpy.Builtins.Int(b));
                }

                return result;
            }

            /// <summary>
            /// Return a copy of the hash object with the same accumulated data.
            /// </summary>
            public HashObject Copy()
            {
                HashObject newObj = new HashObject(this.Name, this.DigestSize);
                foreach (var __loopVar_3 in this._Data)
                {
                    var b = __loopVar_3;
                    newObj._Data.Append(b);
                }

                return newObj;
            }

            public string Name { get; }
            public int DigestSize { get; }

            /// <summary>
            /// Create a new hash object for the specified algorithm.
            /// </summary>
            public HashObject(string algorithmName, int digestSize)
            {
                if (!new Sharpy.List<string>()
                {
                    "md5",
                    "sha1",
                    "sha256",
                    "sha384",
                    "sha512"
                }.Contains(algorithmName))
                {
                    throw new global::Sharpy.ValueError("unsupported hash type '" + algorithmName + "'");
                }

                this.Name = algorithmName;
                this.DigestSize = digestSize;
                this._Data = new Sharpy.List<byte>()
                {
                };
            }
        }

        /// <summary>
        /// Return a new hash object for MD5, optionally initialized with data.
        /// </summary>
        public static HashObject Md5(string data = "")
        {
            HashObject obj = new HashObject("md5", 16);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }

        /// <summary>
        /// Return a new hash object for SHA-1, optionally initialized with data.
        /// </summary>
        public static HashObject Sha1(string data = "")
        {
            HashObject obj = new HashObject("sha1", 20);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }

        /// <summary>
        /// Return a new hash object for SHA-256, optionally initialized with data.
        /// </summary>
        public static HashObject Sha256(string data = "")
        {
            HashObject obj = new HashObject("sha256", 32);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }

        /// <summary>
        /// Return a new hash object for SHA-384, optionally initialized with data.
        /// </summary>
        public static HashObject Sha384(string data = "")
        {
            HashObject obj = new HashObject("sha384", 48);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }

        /// <summary>
        /// Return a new hash object for SHA-512, optionally initialized with data.
        /// </summary>
        public static HashObject Sha512(string data = "")
        {
            HashObject obj = new HashObject("sha512", 64);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }

        /// <summary>
        /// Return a new hash object for SHA-224, optionally initialized with data.
        /// </summary>
        public static HashObject Sha224(string data = "")
        {
            HashObject obj = new HashObject("sha224", 28);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }

        /// <summary>
        /// Return a new hash object for SHA3-256, optionally initialized with data.
        /// </summary>
        public static HashObject Sha3256(string data = "")
        {
            HashObject obj = new HashObject("sha3_256", 32);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }

        /// <summary>
        /// Return a new hash object for SHA3-512, optionally initialized with data.
        /// </summary>
        public static HashObject Sha3512(string data = "")
        {
            HashObject obj = new HashObject("sha3_512", 64);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }

        /// <summary>
        /// Return a new hash object for BLAKE2b, optionally initialized with data.
        /// </summary>
        public static HashObject Blake2b(string data = "")
        {
            HashObject obj = new HashObject("blake2b", 64);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }

        /// <summary>
        /// Return a new hash object for BLAKE2s, optionally initialized with data.
        /// </summary>
        public static HashObject Blake2s(string data = "")
        {
            HashObject obj = new HashObject("blake2s", 32);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }
    }
}
