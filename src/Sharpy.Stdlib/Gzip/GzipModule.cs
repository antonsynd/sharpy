using System;
using System.IO;
using System.IO.Compression;

namespace Sharpy
{
    public static partial class GzipModule
    {
        public static Bytes Compress(Bytes data, int compresslevel = 9)
        {
            if (compresslevel != -1 && (compresslevel < 0 || compresslevel > 9))
            {
                throw new ValueError("Bad compression level: " + compresslevel);
            }

            CompressionLevel level = MapGzipLevel(compresslevel);

            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, level, leaveOpen: true))
                {
                    byte[] bytes = data.ToArray();
                    gzip.Write(bytes, 0, bytes.Length);
                }

                return new Bytes(output.ToArray());
            }
        }

        public static Bytes Decompress(Bytes data)
        {
            try
            {
                byte[] input = data.ToArray();
                using (var inputStream = new MemoryStream(input))
                using (var output = new MemoryStream())
                using (var gzip = new GZipStream(inputStream, CompressionMode.Decompress))
                {
                    gzip.CopyTo(output);
                    return new Bytes(output.ToArray());
                }
            }
            catch (InvalidDataException ex)
            {
                throw new BadGzipFile("Not a gzipped file (" + ex.Message + ")");
            }
        }

        internal static CompressionLevel MapGzipLevel(int level)
        {
            if (level == -1)
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
    }
}
