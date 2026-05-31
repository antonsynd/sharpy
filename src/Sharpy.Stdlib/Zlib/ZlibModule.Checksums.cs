using System;

namespace Sharpy
{
    /// <summary>Provides zlib checksum helpers.</summary>
    public static partial class ZlibModule
    {
        /// <summary>Computes the CRC-32 checksum of the data.</summary>
        public static long Crc32(Bytes data, long value = 0)
        {
            byte[] bytes = data.ToArray();
            uint crc = (uint)(value & 0xFFFFFFFF);
            crc = ~crc;

            for (int i = 0; i < bytes.Length; i++)
            {
                crc = (crc >> 8) ^ Crc32Table[(crc ^ bytes[i]) & 0xFF];
            }

            crc = ~crc;
            return (long)(crc & 0xFFFFFFFF);
        }

        /// <summary>Computes the Adler-32 checksum of the data.</summary>
        public static long Adler32(Bytes data, long value = 1)
        {
            byte[] bytes = data.ToArray();
            uint s1 = (uint)(value & 0xFFFF);
            uint s2 = (uint)((value >> 16) & 0xFFFF);
            const uint MOD_ADLER = 65521;

            for (int i = 0; i < bytes.Length; i++)
            {
                s1 = (s1 + bytes[i]) % MOD_ADLER;
                s2 = (s2 + s1) % MOD_ADLER;
            }

            return (long)(((s2 << 16) | s1) & 0xFFFFFFFF);
        }

        private static readonly uint[] Crc32Table = GenerateCrc32Table();

        private static uint[] GenerateCrc32Table()
        {
            uint[] table = new uint[256];
            const uint polynomial = 0xEDB88320;

            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 1) == 1)
                    {
                        crc = (crc >> 1) ^ polynomial;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }

                table[i] = crc;
            }

            return table;
        }
    }
}
