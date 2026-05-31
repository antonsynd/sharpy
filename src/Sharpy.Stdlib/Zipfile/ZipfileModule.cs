using System;
using System.IO;
using System.IO.Compression;

namespace Sharpy
{
    /// <summary>Provides helpers for working with ZIP archives.</summary>
    public static partial class ZipfileModule
    {
        /// <summary>Returns true if the file is a readable ZIP archive.</summary>
        public static bool IsZipfile(string filename)
        {
            if (!File.Exists(filename))
            {
                return false;
            }

            try
            {
                using (var stream = File.OpenRead(filename))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    _ = archive.Entries.Count;
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Returns true if the bytes contain a readable ZIP archive.</summary>
        public static bool IsZipfile(Bytes data)
        {
            try
            {
                byte[] bytes = data.ToArray();
                using (var stream = new MemoryStream(bytes))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    _ = archive.Entries.Count;
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
