using System;
using System.IO;
using System.IO.Compression;

namespace Sharpy
{
    /// <summary>Provides zlib compression and decompression functions.</summary>
    public static partial class ZlibModule
    {
        /// <summary>Compresses data using zlib format.</summary>
        public static Bytes Compress(Bytes data, int level = 6)
        {
            if (level != -1 && (level < 0 || level > 9))
            {
                throw new ZlibError("Bad compression level");
            }

            CompressionLevel compressionLevel = MapLevel(level);
            return CompressBytes(data.ToArray(), compressionLevel);
        }

        /// <summary>Decompresses zlib, raw deflate, or gzip data depending on wbits.</summary>
        public static Bytes Decompress(Bytes data, int wbits = 15, int bufsize = 16384)
        {
            byte[] input = data.ToArray();

            if (wbits > 16)
            {
                return DecompressGzip(input);
            }

            if (wbits < 0)
            {
                return DecompressRaw(input);
            }

            return DecompressZlib(input);
        }

        /// <summary>Creates an incremental compressor object.</summary>
        public static CompressObj Compressobj(int level = 6, int method = 8, int wbits = 15, int memLevel = 8, int strategy = 0)
        {
            if (level != -1 && (level < 0 || level > 9))
            {
                throw new ZlibError("Bad compression level");
            }

            return new CompressObj(MapLevel(level), wbits);
        }

        /// <summary>Creates an incremental decompressor object.</summary>
        public static DecompressObj Decompressobj(int wbits = 15)
        {
            return new DecompressObj(wbits);
        }

        internal static Bytes CompressBytes(byte[] input, CompressionLevel level)
        {
            using (var output = new MemoryStream())
            {
#if NET10_0_OR_GREATER
                using (var zlib = new ZLibStream(output, level, leaveOpen: true))
                {
                    zlib.Write(input, 0, input.Length);
                }
#else
                WriteZlibHeader(output, level);
                using (var deflate = new DeflateStream(output, level, leaveOpen: true))
                {
                    deflate.Write(input, 0, input.Length);
                }
                WriteAdler32Trailer(output, input);
#endif
                return new Bytes(output.ToArray());
            }
        }

        internal static Bytes DecompressZlib(byte[] input)
        {
            try
            {
                using (var inputStream = new MemoryStream(input))
                using (var output = new MemoryStream())
                {
#if NET10_0_OR_GREATER
                    using (var zlib = new ZLibStream(inputStream, CompressionMode.Decompress))
                    {
                        zlib.CopyTo(output);
                    }
#else
                    if (input.Length < 2)
                    {
                        throw new ZlibError("Error -5 while decompressing data: incomplete or truncated stream");
                    }
                    inputStream.Position = 2;
                    using (var deflate = new DeflateStream(inputStream, CompressionMode.Decompress))
                    {
                        deflate.CopyTo(output);
                    }
#endif
                    return new Bytes(output.ToArray());
                }
            }
            catch (ZlibError)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ZlibError("Error -3 while decompressing data: " + ex.Message);
            }
        }

        internal static Bytes DecompressRaw(byte[] input)
        {
            try
            {
                using (var inputStream = new MemoryStream(input))
                using (var output = new MemoryStream())
                using (var deflate = new DeflateStream(inputStream, CompressionMode.Decompress))
                {
                    deflate.CopyTo(output);
                    return new Bytes(output.ToArray());
                }
            }
            catch (Exception ex)
            {
                throw new ZlibError("Error -3 while decompressing data: " + ex.Message);
            }
        }

        internal static Bytes DecompressGzip(byte[] input)
        {
            try
            {
                using (var inputStream = new MemoryStream(input))
                using (var output = new MemoryStream())
                using (var gzip = new GZipStream(inputStream, CompressionMode.Decompress))
                {
                    gzip.CopyTo(output);
                    return new Bytes(output.ToArray());
                }
            }
            catch (Exception ex)
            {
                throw new ZlibError("Error -3 while decompressing data: " + ex.Message);
            }
        }

        internal static CompressionLevel MapLevel(int level)
        {
            if (level == -1 || level == 6)
            {
                return CompressionLevel.Optimal;
            }

            if (level == 0)
            {
                return CompressionLevel.NoCompression;
            }

            if (level <= 3)
            {
                return CompressionLevel.Fastest;
            }

#if NET10_0_OR_GREATER
            if (level <= 6)
            {
                return CompressionLevel.Optimal;
            }

            return CompressionLevel.SmallestSize;
#else
            return CompressionLevel.Optimal;
#endif
        }

#if !NET10_0_OR_GREATER
        private static void WriteZlibHeader(MemoryStream output, CompressionLevel level)
        {
            byte cmf = 0x78;
            byte flg;
            if (level == CompressionLevel.NoCompression)
            {
                flg = 0x01;
            }
            else if (level == CompressionLevel.Fastest)
            {
                flg = 0x5E;
            }
            else
            {
                flg = 0x9C;
            }

            output.WriteByte(cmf);
            output.WriteByte(flg);
        }

        private static void WriteAdler32Trailer(MemoryStream output, byte[] data)
        {
            uint a = (uint)Adler32(new Bytes(data));
            output.WriteByte((byte)((a >> 24) & 0xFF));
            output.WriteByte((byte)((a >> 16) & 0xFF));
            output.WriteByte((byte)((a >> 8) & 0xFF));
            output.WriteByte((byte)(a & 0xFF));
        }
#endif
    }
}
