using System;
using System.Security.Cryptography;
using System.Text;

namespace Sharpy
{
    public static partial class UuidModule
    {
#pragma warning disable CA1707
        public static readonly UUID NAMESPACE_DNS = new UUID("6ba7b810-9dad-11d1-80b4-00c04fd430c8");
        public static readonly UUID NAMESPACE_URL = new UUID("6ba7b811-9dad-11d1-80b4-00c04fd430c8");
        public static readonly UUID NAMESPACE_OID = new UUID("6ba7b812-9dad-11d1-80b4-00c04fd430c8");
        public static readonly UUID NAMESPACE_X500 = new UUID("6ba7b814-9dad-11d1-80b4-00c04fd430c8");
#pragma warning restore CA1707

        public static UUID Uuid4()
        {
            return new UUID(Guid.NewGuid());
        }

        public static UUID Uuid1()
        {
            var epoch = new System.DateTime(1582, 10, 15, 0, 0, 0, System.DateTimeKind.Utc);
            long timestamp = (System.DateTime.UtcNow - epoch).Ticks;

            var randomBytes = new byte[8];
#if NET10_0_OR_GREATER
            RandomNumberGenerator.Fill(randomBytes);
#else
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
#endif

            int clockSeq = ((randomBytes[0] & 0x3F) << 8) | randomBytes[1];
            var node = new byte[6];
            Array.Copy(randomBytes, 2, node, 0, 6);

            var bytes = new byte[16];

            uint timeLow = (uint)(timestamp & 0xFFFFFFFF);
            bytes[0] = (byte)((timeLow >> 24) & 0xFF);
            bytes[1] = (byte)((timeLow >> 16) & 0xFF);
            bytes[2] = (byte)((timeLow >> 8) & 0xFF);
            bytes[3] = (byte)(timeLow & 0xFF);

            ushort timeMid = (ushort)((timestamp >> 32) & 0xFFFF);
            bytes[4] = (byte)((timeMid >> 8) & 0xFF);
            bytes[5] = (byte)(timeMid & 0xFF);

            ushort timeHi = (ushort)((timestamp >> 48) & 0x0FFF);
            timeHi |= (ushort)(1 << 12);
            bytes[6] = (byte)((timeHi >> 8) & 0xFF);
            bytes[7] = (byte)(timeHi & 0xFF);

            bytes[8] = (byte)(((clockSeq >> 8) & 0x3F) | 0x80);
            bytes[9] = (byte)(clockSeq & 0xFF);

            Array.Copy(node, 0, bytes, 10, 6);

            return UUID.FromRfc4122Bytes(bytes);
        }

        public static UUID Uuid3(UUID namespaceUuid, string name)
        {
            return GenerateNameBased(namespaceUuid, name, 3);
        }

        public static UUID Uuid5(UUID namespaceUuid, string name)
        {
            return GenerateNameBased(namespaceUuid, name, 5);
        }

        private static UUID GenerateNameBased(UUID namespaceUuid, string name, int version)
        {
            byte[] nsBytes = namespaceUuid.ToRfc4122Bytes();
            var nameBytes = Encoding.UTF8.GetBytes(name);

            var input = new byte[nsBytes.Length + nameBytes.Length];
            Array.Copy(nsBytes, 0, input, 0, nsBytes.Length);
            Array.Copy(nameBytes, 0, input, nsBytes.Length, nameBytes.Length);

            byte[] hash;
            if (version == 3)
            {
#pragma warning disable CA5351
                using (var md5 = MD5.Create())
                {
                    hash = md5.ComputeHash(input);
                }
#pragma warning restore CA5351
            }
            else
            {
#pragma warning disable CA5350
                using (var sha1 = SHA1.Create())
                {
                    hash = sha1.ComputeHash(input);
                }
#pragma warning restore CA5350
            }

            var uuidBytes = new byte[16];
            Array.Copy(hash, 0, uuidBytes, 0, 16);

            uuidBytes[6] = (byte)((uuidBytes[6] & 0x0F) | (version << 4));
            uuidBytes[8] = (byte)((uuidBytes[8] & 0x3F) | 0x80);

            return UUID.FromRfc4122Bytes(uuidBytes);
        }
    }
}
