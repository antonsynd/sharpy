using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Represents a ZIP archive for reading, writing, or appending.
    /// Similar to Python's zipfile.ZipFile class.
    /// </summary>
    [SharpyModuleType("zipfile", "ZipFile")]
    public class ZipFile : IDisposable
    {
        private ZipArchive? _archive;
        private FileStream? _stream;
        private readonly string _mode;
        private readonly int _compression;

        /// <summary>
        /// Open a ZIP archive.
        /// </summary>
        /// <param name="file">Path to the ZIP file.</param>
        /// <param name="mode">"r" for reading, "w" for writing, "a" for appending.</param>
        /// <param name="compression">Compression method: ZIP_STORED (0) or ZIP_DEFLATED (8). Default is ZIP_DEFLATED.</param>
        public ZipFile(string file, string mode = "r", int compression = 8)
        {
            _mode = mode;
            _compression = compression;

            ZipArchiveMode archiveMode;
            switch (mode)
            {
                case "r":
                    archiveMode = ZipArchiveMode.Read;
                    _stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                    break;
                case "w":
                    archiveMode = ZipArchiveMode.Create;
                    _stream = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None);
                    break;
                case "a":
                    archiveMode = ZipArchiveMode.Update;
                    _stream = new FileStream(file, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    break;
                default:
                    throw new ArgumentException("Mode must be 'r', 'w', or 'a', got '" + mode + "'");
            }

            _archive = new ZipArchive(_stream, archiveMode, leaveOpen: false);
        }

        /// <summary>Return a list of archive members by name.</summary>
        public List<string> Namelist()
        {
            EnsureOpen();
            var names = new List<string>();
            foreach (var entry in _archive!.Entries)
            {
                names.Add(entry.FullName);
            }
            return names;
        }

        /// <summary>Return a ZipInfo object with information about the archive member name.</summary>
        /// <param name="name">Name of the file in the archive.</param>
        /// <returns>A ZipInfo instance with metadata about the file.</returns>
        public ZipInfo Getinfo(string name)
        {
            EnsureOpen();
            var entry = _archive!.GetEntry(name);
            if (entry == null)
            {
                throw new KeyNotFoundException("There is no item named '" + name + "' in the archive");
            }

            // .NET ZipArchiveEntry doesn't expose compression method directly.
            // Use heuristic: if compressed size equals uncompressed, assume stored.
            int compressType = entry.CompressedLength == entry.Length
                ? ZipfileModule.ZIP_STORED
                : ZipfileModule.ZIP_DEFLATED;

            return new ZipInfo(
                entry.FullName,
                entry.Length,
                entry.CompressedLength,
                compressType,
                entry.LastWriteTime);
        }

        /// <summary>Return the bytes of the file name in the archive.</summary>
        /// <param name="name">Name of the file in the archive.</param>
        /// <returns>The file contents as a byte array.</returns>
        public byte[] Read(string name)
        {
            EnsureOpen();
            if (_mode == "w")
            {
                throw new InvalidOperationException("Cannot read from a ZipFile opened for writing");
            }
            var entry = _archive!.GetEntry(name);
            if (entry == null)
            {
                throw new KeyNotFoundException("There is no item named '" + name + "' in the archive");
            }

            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        /// <summary>Extract all files from the archive to a directory.</summary>
        /// <param name="path">Destination directory. Defaults to current directory.</param>
        public void Extractall(string? path = null)
        {
            EnsureOpen();
            string destDir = path ?? Directory.GetCurrentDirectory();
            Directory.CreateDirectory(destDir);

            foreach (var entry in _archive!.Entries)
            {
                ExtractEntry(entry, destDir);
            }
        }

        /// <summary>Extract a member from the archive to a directory.</summary>
        /// <param name="member">Name of the file in the archive.</param>
        /// <param name="path">Destination directory. Defaults to current directory.</param>
        /// <returns>The path to the extracted file.</returns>
        public string Extract(string member, string? path = null)
        {
            EnsureOpen();
            var entry = _archive!.GetEntry(member);
            if (entry == null)
            {
                throw new KeyNotFoundException("There is no item named '" + member + "' in the archive");
            }

            string destDir = path ?? Directory.GetCurrentDirectory();
            return ExtractEntry(entry, destDir);
        }

        /// <summary>
        /// Write a file into the archive.
        /// </summary>
        /// <param name="filename">Path of the file to add.</param>
        /// <param name="arcname">Name to use in the archive. Defaults to the filename.</param>
        public void Write(string filename, string? arcname = null)
        {
            EnsureOpen();
            EnsureWritable();
            string entryName = arcname ?? System.IO.Path.GetFileName(filename);

            var entry = _archive!.CreateEntry(entryName, GetCompressionLevel());
            using var entryStream = entry.Open();
            using var fileStream = System.IO.File.OpenRead(filename);
            fileStream.CopyTo(entryStream);
        }

        /// <summary>
        /// Write a string into the archive as a file.
        /// </summary>
        /// <param name="arcname">Name of the file in the archive.</param>
        /// <param name="data">String data to write.</param>
        public void Writestr(string arcname, string data)
        {
            EnsureOpen();
            EnsureWritable();

            var entry = _archive!.CreateEntry(arcname, GetCompressionLevel());
            using var entryStream = entry.Open();
            var bytes = Encoding.UTF8.GetBytes(data);
            entryStream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>Close the archive and release resources.</summary>
        public void Close()
        {
            if (_archive != null)
            {
                _archive.Dispose();
                _archive = null;
                // Stream is disposed by ZipArchive (leaveOpen: false)
                _stream = null;
            }
        }

        /// <summary>Dispose the archive (supports context manager pattern).</summary>
        public void Dispose()
        {
            Close();
        }

        private void EnsureOpen()
        {
            if (_archive == null)
            {
                throw new InvalidOperationException("Attempt to use a closed ZipFile");
            }
        }

        private void EnsureWritable()
        {
            if (_mode == "r")
            {
                throw new InvalidOperationException("Cannot write to a ZipFile opened for reading");
            }
        }

        private CompressionLevel GetCompressionLevel()
        {
            return _compression == ZipfileModule.ZIP_STORED
                ? CompressionLevel.NoCompression
                : CompressionLevel.Optimal;
        }

        private static string ExtractEntry(ZipArchiveEntry entry, string destDir)
        {
            string destPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(destDir, entry.FullName));
            string fullDestDir = System.IO.Path.GetFullPath(destDir + System.IO.Path.DirectorySeparatorChar);

            // Prevent ZIP slip: ensure extracted path is within the destination directory
            if (!destPath.StartsWith(fullDestDir, StringComparison.Ordinal))
            {
                throw new IOException("Entry is outside the target directory: " + entry.FullName);
            }

            string? dirPath = System.IO.Path.GetDirectoryName(destPath);
            if (dirPath != null)
            {
                Directory.CreateDirectory(dirPath);
            }

            // Skip directory entries (they end with /)
            if (string.IsNullOrEmpty(entry.Name))
            {
                return destPath;
            }

            using var entryStream = entry.Open();
            using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
            entryStream.CopyTo(fileStream);
            return destPath;
        }
    }
}
