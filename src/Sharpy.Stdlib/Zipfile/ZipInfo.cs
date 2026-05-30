using System;
using System.IO.Compression;

namespace Sharpy
{
    [SharpyModuleType("zipfile", "ZipInfo")]
    public class ZipInfo
    {
        public string Filename { get; set; }
        public List<int> DateTime { get; set; }
        public int CompressType { get; set; }
        public Bytes Comment { get; set; }
        public Bytes Extra { get; set; }
        public int CreateSystem { get; set; }
        public int CreateVersion { get; set; }
        public int ExtractVersion { get; set; }
        public long FileSize { get; set; }
        public long CompressSize { get; set; }
        public long Crc { get; set; }
        public int ExternalAttr { get; set; }
        public int InternalAttr { get; set; }
        public int FlagBits { get; set; }

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

        public bool IsDir()
        {
            return Filename.EndsWith("/");
        }

        public override string ToString()
        {
            return "<ZipInfo filename='" + Filename + "' compress_type=" + CompressType + ">";
        }
    }
}
