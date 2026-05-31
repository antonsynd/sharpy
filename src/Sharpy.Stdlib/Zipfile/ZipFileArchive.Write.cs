using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace Sharpy
{
    /// <summary>Provides write and extraction operations for ZIP archives.</summary>
    public partial class ZipFileArchive
    {
        /// <summary>Adds a file from disk to the archive.</summary>
        public void Write(string filename, string? arcname = null, int? compressType = null)
        {
            EnsureOpen();
            EnsureWritable();
            string entryName = arcname ?? System.IO.Path.GetFileName(filename);
            CompressionLevel level = compressType.HasValue
                ? (compressType.Value == 0 ? CompressionLevel.NoCompression : CompressionLevel.Optimal)
                : GetCompressionLevel();

            var entry = _archive!.CreateEntry(entryName, level);
            using (var entryStream = entry.Open())
            using (var fileStream = System.IO.File.OpenRead(filename))
            {
                fileStream.CopyTo(entryStream);
            }
        }

        /// <summary>Writes bytes to the archive using the supplied ZipInfo metadata.</summary>
        public void Writestr(ZipInfo zinfo, Bytes data, int? compressType = null)
        {
            EnsureOpen();
            EnsureWritable();
            CompressionLevel level = compressType.HasValue
                ? (compressType.Value == 0 ? CompressionLevel.NoCompression : CompressionLevel.Optimal)
                : GetCompressionLevel();

            var entry = _archive!.CreateEntry(zinfo.Filename, level);
            using (var entryStream = entry.Open())
            {
                byte[] bytes = data.ToArray();
                entryStream.Write(bytes, 0, bytes.Length);
            }
        }

        /// <summary>Writes bytes to a named archive member.</summary>
        public void Writestr(string arcname, Bytes data, int? compressType = null)
        {
            Writestr(new ZipInfo(arcname), data, compressType);
        }

        /// <summary>Writes UTF-8 text to a named archive member.</summary>
        public void Writestr(string arcname, string data, int? compressType = null)
        {
            Writestr(arcname, new Bytes(Encoding.UTF8.GetBytes(data)), compressType);
        }

        /// <summary>Extracts one archive member to a target directory.</summary>
        public string Extract(string member, string? path = null)
        {
            EnsureOpen();
            var entry = _archive!.GetEntry(member);
            if (entry == null)
            {
                throw new KeyError("There is no item named '" + member + "' in the archive");
            }

            string destDir = path ?? Directory.GetCurrentDirectory();
            return ExtractEntry(entry, destDir);
        }

        /// <summary>Extracts all members, or the selected members, to a target directory.</summary>
        public void Extractall(string? path = null, List<string>? members = null)
        {
            EnsureOpen();
            string destDir = path ?? Directory.GetCurrentDirectory();
            Directory.CreateDirectory(destDir);

            if (members != null)
            {
                foreach (string name in members)
                {
                    var entry = _archive!.GetEntry(name);
                    if (entry == null)
                    {
                        throw new KeyError("There is no item named '" + name + "' in the archive");
                    }
                    ExtractEntry(entry, destDir);
                }
            }
            else
            {
                foreach (var entry in _archive!.Entries)
                {
                    ExtractEntry(entry, destDir);
                }
            }
        }

        /// <summary>Creates a directory entry in the archive.</summary>
        public void Mkdir(string zinfOrArcname)
        {
            EnsureOpen();
            EnsureWritable();
            string dirName = zinfOrArcname.EndsWith("/") ? zinfOrArcname : zinfOrArcname + "/";
            _archive!.CreateEntry(dirName);
        }

        private static string ExtractEntry(ZipArchiveEntry entry, string destDir)
        {
            string destPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(destDir, entry.FullName));
            string fullDestDir = System.IO.Path.GetFullPath(destDir + System.IO.Path.DirectorySeparatorChar);

            var comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!destPath.StartsWith(fullDestDir, comparison))
            {
                throw new ValueError("Entry is outside the target directory: " + entry.FullName);
            }

            string? dirPath = System.IO.Path.GetDirectoryName(destPath);
            if (dirPath != null)
            {
                Directory.CreateDirectory(dirPath);
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                return destPath;
            }

            using (var entryStream = entry.Open())
            using (var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                entryStream.CopyTo(fileStream);
            }
            return destPath;
        }
    }
}
