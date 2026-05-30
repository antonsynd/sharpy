using System;
using System.IO;
using System.IO.Compression;

namespace Sharpy
{
    public static partial class ZipfileModule
    {
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
