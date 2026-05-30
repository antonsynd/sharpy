using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Sharpy
{
    [SharpyModuleType("hmac")]
    public sealed class HmacObject
    {
        private readonly string _algorithmName;
        private readonly byte[] _key;
        private readonly System.Collections.Generic.List<byte> _data;

        public int DigestSize { get; }

        public string Name => "hmac-" + _algorithmName;

        public int BlockSize
        {
            get
            {
                return _algorithmName switch
                {
                    "sha384" => 128,
                    "sha512" => 128,
                    _ => 64
                };
            }
        }

        public HmacObject(string key, string algorithmName, int digestSize)
        {
            _key = Encoding.UTF8.GetBytes(key);
            _algorithmName = algorithmName;
            DigestSize = digestSize;
            _data = new System.Collections.Generic.List<byte>();
        }

        internal HmacObject(byte[] key, string algorithmName, int digestSize)
        {
            _key = key;
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

        public void Update(string data)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            _data.AddRange(bytes);
        }

        public void Update(Bytes data)
        {
            _data.AddRange(data.ToArray());
        }

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

        public HmacObject Copy()
        {
            return new HmacObject(_key, _algorithmName, DigestSize, _data);
        }

        private byte[] ComputeHmac()
        {
            using (var hmac = CreateHmacAlgorithm(_algorithmName, _key))
            {
                return hmac.ComputeHash(_data.ToArray());
            }
        }

        internal static HMAC CreateHmacAlgorithm(string algorithmName, byte[] key)
        {
            return algorithmName switch
            {
                "md5" => new HMACMD5(key),
                "sha1" => new HMACSHA1(key),
                "sha256" => new HMACSHA256(key),
                "sha384" => new HMACSHA384(key),
                "sha512" => new HMACSHA512(key),
                _ => throw new ValueError("unsupported hash type '" + algorithmName + "'")
            };
        }

        internal static int GetDigestSize(string algorithmName)
        {
            return algorithmName switch
            {
                "md5" => 16,
                "sha1" => 20,
                "sha256" => 32,
                "sha384" => 48,
                "sha512" => 64,
                _ => throw new ValueError("unsupported hash type '" + algorithmName + "'")
            };
        }
    }
}
