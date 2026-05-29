using System;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;

namespace Sharpy
{
    /// <summary>Provides functions for working with tar archives, similar to Python's tarfile module.</summary>
    public static partial class TarfileModule
    {
        /// <summary>
        /// Open a tar archive for reading or writing.
        /// </summary>
        /// <param name="name">The path to the tar archive file.</param>
        /// <param name="mode">The mode to open the archive in. Supported: "r:", "r:gz", "r:bz2", "r:xz", "w:", "w:gz", "w:bz2", "w:xz".
        /// If only "r" or "w" is specified, no compression is assumed.</param>
        /// <returns>A <see cref="TarFile"/> instance.</returns>
        public static TarFile Open(string name, string mode = "r:")
        {
            // Normalize: "r" -> "r:", "w" -> "w:"
            if (mode == "r") mode = "r:";
            if (mode == "w") mode = "w:";
            return new TarFile(name, mode);
        }

        /// <summary>
        /// Return True if name appears to be a tar archive.
        /// </summary>
        /// <param name="name">The path to the file to check.</param>
        /// <returns>True if the file is a valid tar archive, False otherwise.</returns>
        public static bool Is_tarfile(string name)
        {
            if (!File.Exists(name)) return false;

            try
            {
                using var stream = new FileStream(name, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var reader = new TarReader(stream, leaveOpen: false);
                // Try to read at least one entry
                TarEntry? entry = reader.GetNextEntry();
                return entry != null;
            }
            catch
            {
                // Try with gzip decompression
                try
                {
                    using var stream = new FileStream(name, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var gzStream = new GZipStream(stream, CompressionMode.Decompress);
                    using var reader = new TarReader(gzStream, leaveOpen: false);
                    TarEntry? entry = reader.GetNextEntry();
                    return entry != null;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
