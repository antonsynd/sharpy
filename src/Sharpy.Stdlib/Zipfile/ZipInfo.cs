using System;
using System.IO.Compression;

namespace Sharpy
{
    /// <summary>Stores metadata describing a ZIP archive member.</summary>
    [SharpyModuleType("zipfile", "ZipInfo")]
    public class ZipInfo
    {
        /// <summary>Gets or sets the archive member name.</summary>
        public string Filename { get; set; }

        /// <summary>Gets or sets the last modified timestamp as a date-time sequence.</summary>
        public List<int> DateTime { get; set; }

        /// <summary>Gets or sets the ZIP compression method.</summary>
        public int CompressType { get; set; }

        /// <summary>Gets or sets the per-entry comment bytes.</summary>
        public Bytes Comment { get; set; }

        /// <summary>Gets or sets the extra field bytes.</summary>
        public Bytes Extra { get; set; }

        /// <summary>Gets or sets the originating system identifier.</summary>
        public int CreateSystem { get; set; }

        /// <summary>Gets or sets the ZIP version that created the entry.</summary>
        public int CreateVersion { get; set; }

        /// <summary>Gets or sets the ZIP version needed to extract the entry.</summary>
        public int ExtractVersion { get; set; }

        /// <summary>Gets or sets the uncompressed file size.</summary>
        public long FileSize { get; set; }

        /// <summary>Gets or sets the compressed file size.</summary>
        public long CompressSize { get; set; }

        /// <summary>Gets or sets the CRC-32 checksum.</summary>
        public long Crc { get; set; }

        /// <summary>Gets or sets the external file attributes.</summary>
        public int ExternalAttr { get; set; }

        /// <summary>Gets or sets the internal file attributes.</summary>
        public int InternalAttr { get; set; }

        /// <summary>Gets or sets the ZIP general purpose flag bits.</summary>
        public int FlagBits { get; set; }

        /// <summary>Initializes ZIP member metadata with default values.</summary>
        public ZipInfo(string filename = "NoName")
        {
            Filename = filename;
            var now = System.DateTime.Now;
            DateTime = new List<int>(new int[] { now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second });
            CompressType = 0;
            Comment = new Bytes(System.Array.Empty<byte>());
            Extra = new Bytes(System.Array.Empty<byte>());
            CreateSystem = 0;
            CreateVersion = 20;
            ExtractVersion = 20;
            FileSize = 0;
            CompressSize = 0;
            Crc = 0;
            ExternalAttr = 0;
            InternalAttr = 0;
            FlagBits = 0;
        }

        internal static ZipInfo FromEntry(ZipArchiveEntry entry)
        {
            int compressType = entry.CompressedLength == entry.Length
                ? 0
                : 8;

            var info = new ZipInfo(entry.FullName);
            info.FileSize = entry.Length;
            info.CompressSize = entry.CompressedLength;
            info.CompressType = compressType;

            var lw = entry.LastWriteTime;
            info.DateTime = new List<int>(new int[] { lw.Year, lw.Month, lw.Day, lw.Hour, lw.Minute, lw.Second });

            return info;
        }

        /// <summary>Returns true if the entry represents a directory.</summary>
        public bool IsDir()
        {
            return Filename.EndsWith("/");
        }

        /// <summary>Returns the Python-style string representation of the ZIP entry.</summary>
        public override string ToString()
        {
            return "<ZipInfo filename='" + Filename + "' compress_type=" + CompressType + ">";
        }
    }
}
