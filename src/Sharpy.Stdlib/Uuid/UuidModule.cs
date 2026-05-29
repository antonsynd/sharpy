using System;
using System.Security.Cryptography;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// UUID generation and parsing functions, mirroring Python's uuid module.
    /// </summary>
    public static partial class UuidModule
    {
        /// <summary>UUID namespace for DNS names.</summary>
        public static readonly UUID NAMESPACE_DNS = new UUID("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

        /// <summary>UUID namespace for URLs.</summary>
        public static readonly UUID NAMESPACE_URL = new UUID("6ba7b811-9dad-11d1-80b4-00c04fd430c8");

        /// <summary>UUID namespace for ISO OIDs.</summary>
        public static readonly UUID NAMESPACE_OID = new UUID("6ba7b812-9dad-11d1-80b4-00c04fd430c8");

        /// <summary>UUID namespace for X.500 DNs.</summary>
        public static readonly UUID NAMESPACE_X500 = new UUID("6ba7b814-9dad-11d1-80b4-00c04fd430c8");

        /// <summary>
        /// Generate a random UUID (version 4).
        /// Maps to System.Guid.NewGuid().
        /// </summary>
        /// <returns>A new random UUID.</returns>
        public static UUID Uuid4()
        {
            return new UUID(Guid.NewGuid());
        }

        /// <summary>
        /// Generate a UUID based on the host ID and current time (version 1).
        /// Uses a random node ID and clock sequence for privacy.
        /// </summary>
        /// <returns>A new time-based UUID.</returns>
        public static UUID Uuid1()
        {
            // UUID version 1: time-based
            // Timestamp: 100-ns intervals since 1582-10-15
            var epoch = new System.DateTime(1582, 10, 15, 0, 0, 0, System.DateTimeKind.Utc);
            long timestamp = (System.DateTime.UtcNow - epoch).Ticks;

            // Random clock_seq (14 bits) and node (48 bits) for privacy
            var randomBytes = new byte[8];
            RandomNumberGenerator.Fill(randomBytes);

            int clockSeq = ((randomBytes[0] & 0x3F) << 8) | randomBytes[1];
            var node = new byte[6];
            Array.Copy(randomBytes, 2, node, 0, 6);

            // Build 16-byte UUID in RFC 4122 order
            var bytes = new byte[16];

            // time_low (32 bits) - bytes 0-3
            uint timeLow = (uint)(timestamp & 0xFFFFFFFF);
            bytes[0] = (byte)((timeLow >> 24) & 0xFF);
            bytes[1] = (byte)((timeLow >> 16) & 0xFF);
            bytes[2] = (byte)((timeLow >> 8) & 0xFF);
            bytes[3] = (byte)(timeLow & 0xFF);

            // time_mid (16 bits) - bytes 4-5
            ushort timeMid = (ushort)((timestamp >> 32) & 0xFFFF);
            bytes[4] = (byte)((timeMid >> 8) & 0xFF);
            bytes[5] = (byte)(timeMid & 0xFF);

            // time_hi_and_version (16 bits) - bytes 6-7
            ushort timeHi = (ushort)((timestamp >> 48) & 0x0FFF);
            timeHi |= (ushort)(1 << 12); // version 1
            bytes[6] = (byte)((timeHi >> 8) & 0xFF);
            bytes[7] = (byte)(timeHi & 0xFF);

            // clock_seq_hi_and_variant (8 bits) - byte 8
            bytes[8] = (byte)(((clockSeq >> 8) & 0x3F) | 0x80); // variant 10x

            // clock_seq_low (8 bits) - byte 9
            bytes[9] = (byte)(clockSeq & 0xFF);

            // node (48 bits) - bytes 10-15
            Array.Copy(node, 0, bytes, 10, 6);

            return UUID.FromRfc4122Bytes(bytes);
        }

        /// <summary>
        /// Generate a UUID based on the MD5 hash of a namespace UUID and a name (version 3).
        /// </summary>
        /// <param name="namespace_uuid">The namespace UUID.</param>
        /// <param name="name">The name string.</param>
        /// <returns>A new name-based UUID using MD5.</returns>
        public static UUID Uuid3(UUID namespace_uuid, string name)
        {
            return GenerateNameBased(namespace_uuid, name, 3);
        }

        /// <summary>
        /// Generate a UUID based on the SHA-1 hash of a namespace UUID and a name (version 5).
        /// </summary>
        /// <param name="namespace_uuid">The namespace UUID.</param>
        /// <param name="name">The name string.</param>
        /// <returns>A new name-based UUID using SHA-1.</returns>
        public static UUID Uuid5(UUID namespace_uuid, string name)
        {
            return GenerateNameBased(namespace_uuid, name, 5);
        }

        private static UUID GenerateNameBased(UUID namespaceUuid, string name, int version)
        {
            // Get namespace bytes in RFC 4122 order
            var nsBytes = new byte[16];
            var nsList = namespaceUuid.Bytes;
            for (int i = 0; i < 16; i++)
            {
                nsBytes[i] = (byte)nsList[i];
            }

            var nameBytes = Encoding.UTF8.GetBytes(name);

            // Concatenate namespace bytes + name bytes
            var input = new byte[nsBytes.Length + nameBytes.Length];
            Array.Copy(nsBytes, 0, input, 0, nsBytes.Length);
            Array.Copy(nameBytes, 0, input, nsBytes.Length, nameBytes.Length);

            byte[] hash;
            if (version == 3)
            {
                using (var md5 = MD5.Create())
                {
                    hash = md5.ComputeHash(input);
                }
            }
            else
            {
                using (var sha1 = SHA1.Create())
                {
                    hash = sha1.ComputeHash(input);
                }
            }

            // Take first 16 bytes
            var uuidBytes = new byte[16];
            Array.Copy(hash, 0, uuidBytes, 0, 16);

            // Set version
            uuidBytes[6] = (byte)((uuidBytes[6] & 0x0F) | (version << 4));

            // Set variant (RFC 4122: 10xx xxxx)
            uuidBytes[8] = (byte)((uuidBytes[8] & 0x3F) | 0x80);

            return UUID.FromRfc4122Bytes(uuidBytes);
        }
    }
}
