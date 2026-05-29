using System;
using System.IO;
using System.IO.Compression;

namespace Sharpy
{
    public static partial class ZlibModule
    {
        /// <summary>Default compression level (6, matching Python).</summary>
        public static int Z_DEFAULT_COMPRESSION => 6;

        /// <summary>No compression.</summary>
        public static int Z_NO_COMPRESSION => 0;

        /// <summary>Best speed compression.</summary>
        public static int Z_BEST_SPEED => 1;

        /// <summary>Best compression.</summary>
        public static int Z_BEST_COMPRESSION => 9;

        /// <summary>
        /// Compress data and return a bytes object containing the compressed data.
        /// </summary>
        /// <param name="data">The data to compress.</param>
        /// <param name="level">Compression level from 0 (no compression) to 9 (maximum). Default is 6.</param>
        /// <returns>The compressed data as a byte array.</returns>
        public static byte[] Compress(byte[] data, int level = 6)
        {
            if (data == null)
            {
                throw new TypeError("a bytes-like object is required, not 'NoneType'");
            }

            if (level < 0 || level > 9)
            {
                throw new ValueError($"Bad compression level: {level}");
            }

            CompressionLevel compressionLevel = MapLevel(level);
            return CompressBytes(data, compressionLevel);
        }

        /// <summary>
        /// Decompress data and return a bytes object containing the uncompressed data.
        /// </summary>
        /// <param name="data">The compressed data to decompress.</param>
        /// <returns>The decompressed data as a byte array.</returns>
        public static byte[] Decompress(byte[] data)
        {
            if (data == null)
            {
                throw new TypeError("a bytes-like object is required, not 'NoneType'");
            }

            return DecompressBytes(data);
        }

        /// <summary>
        /// Compute a CRC-32 checksum of data.
        /// </summary>
        /// <param name="data">The data to checksum.</param>
        /// <param name="value">Starting CRC value (default 0).</param>
        /// <returns>The CRC-32 checksum as an unsigned 32-bit integer.</returns>
        public static long Crc32(byte[] data, long value = 0)
        {
            if (data == null)
            {
                throw new TypeError("a bytes-like object is required, not 'NoneType'");
            }

            uint crc = (uint)(value & 0xFFFFFFFF);
            crc = ~crc;

            for (int i = 0; i < data.Length; i++)
            {
                crc = (crc >> 8) ^ Crc32Table[(crc ^ data[i]) & 0xFF];
            }

            crc = ~crc;
            return (long)(crc & 0xFFFFFFFF);
        }

        /// <summary>
        /// Compute an Adler-32 checksum of data.
        /// </summary>
        /// <param name="data">The data to checksum.</param>
        /// <param name="value">Starting Adler value (default 1).</param>
        /// <returns>The Adler-32 checksum as an unsigned 32-bit integer.</returns>
        public static long Adler32(byte[] data, long value = 1)
        {
            if (data == null)
            {
                throw new TypeError("a bytes-like object is required, not 'NoneType'");
            }

            uint s1 = (uint)(value & 0xFFFF);
            uint s2 = (uint)((value >> 16) & 0xFFFF);
            const uint MOD_ADLER = 65521;

            for (int i = 0; i < data.Length; i++)
            {
                s1 = (s1 + data[i]) % MOD_ADLER;
                s2 = (s2 + s1) % MOD_ADLER;
            }

            return (long)(((s2 << 16) | s1) & 0xFFFFFFFF);
        }

        /// <summary>
        /// Create a streaming compression object.
        /// </summary>
        /// <param name="level">Compression level from 0 to 9. Default is 6.</param>
        /// <returns>A new <see cref="CompressObj"/> for streaming compression.</returns>
        public static CompressObj Compressobj(int level = 6)
        {
            if (level < 0 || level > 9)
            {
                throw new ValueError($"Bad compression level: {level}");
            }

            return new CompressObj(MapLevel(level));
        }

        /// <summary>
        /// Create a streaming decompression object.
        /// </summary>
        /// <returns>A new <see cref="DecompressObj"/> for streaming decompression.</returns>
        public static DecompressObj Decompressobj()
        {
            return new DecompressObj();
        }

        internal static byte[] CompressBytes(byte[] input, CompressionLevel level)
        {
            using (var output = new MemoryStream())
            {
#if NET6_0_OR_GREATER
                using (var zlib = new ZLibStream(output, level, leaveOpen: true))
                {
                    zlib.Write(input, 0, input.Length);
                }
#else
                // netstandard2.1: use DeflateStream (no ZLibStream available)
                using (var deflate = new DeflateStream(output, level, leaveOpen: true))
                {
                    deflate.Write(input, 0, input.Length);
                }
#endif
                return output.ToArray();
            }
        }

        internal static byte[] DecompressBytes(byte[] input)
        {
            using (var inputStream = new MemoryStream(input))
            using (var output = new MemoryStream())
            {
#if NET6_0_OR_GREATER
                using (var zlib = new ZLibStream(inputStream, CompressionMode.Decompress))
                {
                    zlib.CopyTo(output);
                }
#else
                // netstandard2.1: use DeflateStream (no ZLibStream available)
                using (var deflate = new DeflateStream(inputStream, CompressionMode.Decompress))
                {
                    deflate.CopyTo(output);
                }
#endif
                return output.ToArray();
            }
        }

        private static CompressionLevel MapLevel(int level)
        {
            if (level == 0)
            {
                return CompressionLevel.NoCompression;
            }

            if (level <= 3)
            {
                return CompressionLevel.Fastest;
            }

#if NET6_0_OR_GREATER
            if (level <= 6)
            {
                return CompressionLevel.Optimal;
            }

            return CompressionLevel.SmallestSize;
#else
            return CompressionLevel.Optimal;
#endif
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
