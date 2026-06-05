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
        /// Return a new hash object for MD5, optionally initialized with data.
        /// </summary>
        public static global::Sharpy.HashObject Md5(string data = "")
        {
            global::Sharpy.HashObject obj = new global::Sharpy.HashObject("md5", 16);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }

        /// <summary>
        /// Return a new hash object for SHA-1, optionally initialized with data.
        /// </summary>
        public static global::Sharpy.HashObject Sha1(string data = "")
        {
            global::Sharpy.HashObject obj = new global::Sharpy.HashObject("sha1", 20);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }

        /// <summary>
        /// Return a new hash object for SHA-256, optionally initialized with data.
        /// </summary>
        public static global::Sharpy.HashObject Sha256(string data = "")
        {
            global::Sharpy.HashObject obj = new global::Sharpy.HashObject("sha256", 32);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }

        /// <summary>
        /// Return a new hash object for SHA-384, optionally initialized with data.
        /// </summary>
        public static global::Sharpy.HashObject Sha384(string data = "")
        {
            global::Sharpy.HashObject obj = new global::Sharpy.HashObject("sha384", 48);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }

        /// <summary>
        /// Return a new hash object for SHA-512, optionally initialized with data.
        /// </summary>
        public static global::Sharpy.HashObject Sha512(string data = "")
        {
            global::Sharpy.HashObject obj = new global::Sharpy.HashObject("sha512", 64);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }

        /// <summary>
        /// Return a new hash object for SHA-224, optionally initialized with data.
        /// </summary>
        public static global::Sharpy.HashObject Sha224(string data = "")
        {
            global::Sharpy.HashObject obj = new global::Sharpy.HashObject("sha224", 28);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }

        /// <summary>
        /// Return a new hash object for SHA3-256, optionally initialized with data.
        /// </summary>
        public static global::Sharpy.HashObject Sha3256(string data = "")
        {
            global::Sharpy.HashObject obj = new global::Sharpy.HashObject("sha3_256", 32);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }

        /// <summary>
        /// Return a new hash object for SHA3-512, optionally initialized with data.
        /// </summary>
        public static global::Sharpy.HashObject Sha3512(string data = "")
        {
            global::Sharpy.HashObject obj = new global::Sharpy.HashObject("sha3_512", 64);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }

        /// <summary>
        /// Return a new hash object for BLAKE2b, optionally initialized with data.
        /// </summary>
        public static global::Sharpy.HashObject Blake2b(string data = "")
        {
            global::Sharpy.HashObject obj = new global::Sharpy.HashObject("blake2b", 64);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }

        /// <summary>
        /// Return a new hash object for BLAKE2s, optionally initialized with data.
        /// </summary>
        public static global::Sharpy.HashObject Blake2s(string data = "")
        {
            global::Sharpy.HashObject obj = new global::Sharpy.HashObject("blake2s", 32);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }
    }
}
