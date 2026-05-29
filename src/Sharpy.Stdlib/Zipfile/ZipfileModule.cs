using System;
using System.IO;
using System.IO.Compression;

namespace Sharpy
{
    /// <summary>Provides functions for working with ZIP archives, similar to Python's zipfile module.</summary>
    public static partial class ZipfileModule
    {
        /// <summary>Compression method: no compression (stored).</summary>
        public static readonly int ZIP_STORED = 0;

        /// <summary>Compression method: deflate compression.</summary>
        public static readonly int ZIP_DEFLATED = 8;

        /// <summary>
        /// Check if a file is a valid ZIP archive.
        /// </summary>
        /// <param name="filename">Path to the file to check.</param>
        /// <returns>True if the file is a valid ZIP archive, false otherwise.</returns>
        public static bool IsZipfile(string filename)
        {
            if (!File.Exists(filename))
            {
                return false;
            }

            try
            {
                using var stream = File.OpenRead(filename);
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                // If we can open and iterate, it's a valid zip
                _ = archive.Entries.Count;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
