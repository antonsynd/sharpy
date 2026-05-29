using System;

namespace Sharpy
{
    /// <summary>Metadata about a file in a ZIP archive, similar to Python's zipfile.ZipInfo.</summary>
    [SharpyModuleType("zipfile", "ZipInfo")]
    public class ZipInfo
    {
        /// <summary>Name of the file in the archive.</summary>
        public string Filename { get; }

        /// <summary>Uncompressed size of the file in bytes.</summary>
        public long FileSize { get; }

        /// <summary>Compressed size of the file in bytes.</summary>
        public long CompressSize { get; }

        /// <summary>Compression method used (ZIP_STORED=0 or ZIP_DEFLATED=8).</summary>
        public int CompressType { get; }

        /// <summary>
        /// Date and time of last modification as a tuple (year, month, day, hour, minute, second).
        /// </summary>
        public (int Year, int Month, int Day, int Hour, int Minute, int Second) DateTime { get; }

        internal ZipInfo(string filename, long fileSize, long compressSize, int compressType, DateTimeOffset lastModified)
        {
            Filename = filename;
            FileSize = fileSize;
            CompressSize = compressSize;
            CompressType = compressType;
            DateTime = (lastModified.Year, lastModified.Month, lastModified.Day,
                        lastModified.Hour, lastModified.Minute, lastModified.Second);
        }
    }
}
