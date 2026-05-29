using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;

namespace Sharpy
{
    /// <summary>
    /// Represents a tar archive for reading or writing, similar to Python's tarfile.TarFile.
    /// Implements IDisposable for use in context managers (with statements).
    /// </summary>
    [SharpyModuleType("tarfile")]
    public sealed class TarFile : IDisposable
    {
        private readonly string _mode;
        private readonly string _name;
        private Stream? _baseStream;
        private Stream? _compressionStream;
        private TarReader? _reader;
        private TarWriter? _writer;
        private bool _disposed;
        private List<TarInfo>? _cachedMembers;

        internal TarFile(string name, string mode)
        {
            _name = name;
            _mode = mode;

            string baseMode = mode.Contains(":") ? mode.Split(':')[0] : mode;
            string compression = mode.Contains(":") ? mode.Split(':')[1] : "";

            if (baseMode == "r")
            {
                _baseStream = new FileStream(name, FileMode.Open, FileAccess.Read, FileShare.Read);
                Stream readStream = WrapDecompressionStream(_baseStream, compression);
                _reader = new TarReader(readStream, leaveOpen: false);
            }
            else if (baseMode == "w")
            {
                _baseStream = new FileStream(name, FileMode.Create, FileAccess.Write, FileShare.None);
                Stream writeStream = WrapCompressionStream(_baseStream, compression);
                _compressionStream = writeStream;
                _writer = new TarWriter(writeStream, leaveOpen: false);
            }
            else
            {
                throw new ValueError("Invalid mode: '" + mode + "'");
            }
        }

        /// <summary>Gets the name of the archive file.</summary>
        public string Name => _name;

        /// <summary>Gets the mode the archive was opened with.</summary>
        public string Mode => _mode;

        /// <summary>
        /// Extract all members from the archive to the specified directory.
        /// </summary>
        /// <param name="path">The directory to extract to. Defaults to current directory.</param>
        public void Extractall(string? path = null)
        {
            EnsureNotDisposed();
            EnsureReadMode();
            string targetDir = path ?? ".";
            Directory.CreateDirectory(targetDir);

            foreach (TarEntry entry in ReadAllEntries())
            {
                ExtractEntry(entry, targetDir);
            }
        }

        /// <summary>
        /// Extract a single member from the archive as a byte array.
        /// Returns the file content as bytes, or null if the member is not a regular file.
        /// </summary>
        /// <param name="member">The name of the member to extract.</param>
        /// <returns>The file content as a byte array, or null for non-file members.</returns>
        public byte[]? Extractfile(string member)
        {
            EnsureNotDisposed();
            EnsureReadMode();

            foreach (TarEntry entry in ReadAllEntries())
            {
                if (entry.Name == member || entry.Name == "./" + member || entry.Name.TrimEnd('/') == member)
                {
                    if (entry.DataStream != null)
                    {
                        using var ms = new MemoryStream();
                        entry.DataStream.CopyTo(ms);
                        return ms.ToArray();
                    }
                    return null;
                }
            }

            throw new KeyError("Member not found: '" + member + "'");
        }

        /// <summary>
        /// Return a list of names of the archive members.
        /// </summary>
        public List<string> Getnames()
        {
            EnsureNotDisposed();
            EnsureReadMode();
            var names = new List<string>();
            foreach (TarInfo info in Getmembers())
            {
                names.Add(info.Name);
            }
            return names;
        }

        /// <summary>
        /// Return a list of TarInfo objects for all archive members.
        /// </summary>
        public List<TarInfo> Getmembers()
        {
            EnsureNotDisposed();
            EnsureReadMode();
            if (_cachedMembers != null) return _cachedMembers;

            _cachedMembers = new List<TarInfo>();
            foreach (TarEntry entry in ReadAllEntries())
            {
                _cachedMembers.Add(EntryToTarInfo(entry));
            }
            return _cachedMembers;
        }

        /// <summary>
        /// Add a file to the archive.
        /// </summary>
        /// <param name="name">The path of the file to add.</param>
        /// <param name="arcname">The name to use in the archive. If null, uses the original name.</param>
        public void Add(string name, string? arcname = null)
        {
            EnsureNotDisposed();
            EnsureWriteMode();
            string archiveName = arcname ?? name;

            if (Directory.Exists(name))
            {
                AddDirectory(name, archiveName);
            }
            else if (File.Exists(name))
            {
                _writer!.WriteEntry(name, archiveName);
            }
            else
            {
                throw new FileNotFoundError("No such file or directory: '" + name + "'");
            }
        }

        /// <summary>
        /// Close the archive.
        /// </summary>
        public void Close()
        {
            Dispose();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _reader?.Dispose();
            _reader = null;

            _writer?.Dispose();
            _writer = null;

            // For write mode with compression, the compression stream
            // needs to be flushed/disposed before the base stream
            if (_compressionStream != null && _compressionStream != _baseStream)
            {
                _compressionStream.Dispose();
                _compressionStream = null;
            }

            _baseStream?.Dispose();
            _baseStream = null;
        }

        // Used for Python-style `with` statement support (context manager)
        internal TarFile __Enter__() => this;
        internal void __Exit__() => Dispose();

        private void AddDirectory(string dirPath, string arcPath)
        {
            foreach (string file in Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories))
            {
                string relativePath = System.IO.Path.GetRelativePath(dirPath, file);
                string entryName = string.IsNullOrEmpty(arcPath)
                    ? relativePath
                    : System.IO.Path.Combine(arcPath, relativePath);
                // Normalize path separators to forward slash for tar convention
                entryName = entryName.Replace('\\', '/');
                _writer!.WriteEntry(file, entryName);
            }
        }

        private IEnumerable<TarEntry> ReadAllEntries()
        {
            // If we already have cached members, we need to re-read from stream.
            // Since TarReader is forward-only, we reopen the archive.
            if (_reader == null)
            {
                // Reopen for reading
                _baseStream?.Dispose();
                string compression = _mode.Contains(":") ? _mode.Split(':')[1] : "";
                _baseStream = new FileStream(_name, FileMode.Open, FileAccess.Read, FileShare.Read);
                Stream readStream = WrapDecompressionStream(_baseStream, compression);
                _reader = new TarReader(readStream, leaveOpen: false);
            }

            TarEntry? entry;
            while ((entry = _reader.GetNextEntry()) != null)
            {
                yield return entry;
            }

            // After exhausting the reader, dispose and null it so next call reopens
            _reader.Dispose();
            _reader = null;
        }

        private static void ExtractEntry(TarEntry entry, string targetDir)
        {
            string entryPath = entry.Name;
            // Sanitize: prevent path traversal
            if (entryPath.Contains(".."))
            {
                throw new ValueError("Tar entry name contains '..': '" + entryPath + "'");
            }

            string fullPath = System.IO.Path.Combine(targetDir, entryPath);
            // Normalize
            fullPath = System.IO.Path.GetFullPath(fullPath);

            // Ensure the path is still within the target directory
            string fullTargetDir = System.IO.Path.GetFullPath(targetDir);
            if (!fullPath.StartsWith(fullTargetDir, StringComparison.Ordinal))
            {
                throw new ValueError("Tar entry would extract outside target directory: '" + entryPath + "'");
            }

            if (entry.EntryType == TarEntryType.Directory)
            {
                Directory.CreateDirectory(fullPath);
            }
            else if (entry.EntryType == TarEntryType.RegularFile || entry.EntryType == TarEntryType.V7RegularFile)
            {
                string? dir = System.IO.Path.GetDirectoryName(fullPath);
                if (dir != null) Directory.CreateDirectory(dir);
                entry.ExtractToFile(fullPath, overwrite: true);
            }
            else if (entry.EntryType == TarEntryType.SymbolicLink)
            {
                string? dir = System.IO.Path.GetDirectoryName(fullPath);
                if (dir != null) Directory.CreateDirectory(dir);
                // Create symbolic link
                if (entry.LinkName != null)
                {
                    File.CreateSymbolicLink(fullPath, entry.LinkName);
                }
            }
        }

        private static TarInfo EntryToTarInfo(TarEntry entry)
        {
            bool isFile = entry.EntryType == TarEntryType.RegularFile || entry.EntryType == TarEntryType.V7RegularFile;
            bool isDir = entry.EntryType == TarEntryType.Directory;
            bool isSym = entry.EntryType == TarEntryType.SymbolicLink;
            double mtime = entry.ModificationTime.ToUnixTimeSeconds();
            return new TarInfo(
                name: entry.Name,
                size: entry.Length,
                mtime: mtime,
                isfile: isFile,
                isdir: isDir,
                issym: isSym,
                linkname: entry.LinkName ?? ""
            );
        }

        private static Stream WrapDecompressionStream(Stream baseStream, string compression)
        {
            switch (compression)
            {
                case "gz":
                    return new GZipStream(baseStream, CompressionMode.Decompress);
                case "bz2":
                    return new BrotliStream(baseStream, CompressionMode.Decompress);
                case "xz":
                    // .NET 10 has ZLibStream; for xz we use the raw stream as fallback
                    // Note: True xz support requires a third-party library.
                    // For now, we treat xz as uncompressed (matching .NET limitations).
                    return baseStream;
                case "":
                    return baseStream;
                default:
                    throw new ValueError("Unsupported compression: '" + compression + "'");
            }
        }

        private static Stream WrapCompressionStream(Stream baseStream, string compression)
        {
            switch (compression)
            {
                case "gz":
                    return new GZipStream(baseStream, CompressionMode.Compress);
                case "bz2":
                    return new BrotliStream(baseStream, CompressionLevel.Optimal);
                case "xz":
                    // Note: True xz support requires a third-party library.
                    return baseStream;
                case "":
                    return baseStream;
                default:
                    throw new ValueError("Unsupported compression: '" + compression + "'");
            }
        }

        private void EnsureNotDisposed()
        {
            if (_disposed) throw new ValueError("I/O operation on closed tar archive.");
        }

        private void EnsureReadMode()
        {
            string baseMode = _mode.Contains(":") ? _mode.Split(':')[0] : _mode;
            if (baseMode != "r")
            {
                throw new ValueError("Cannot read from a tar archive opened for writing.");
            }
        }

        private void EnsureWriteMode()
        {
            string baseMode = _mode.Contains(":") ? _mode.Split(':')[0] : _mode;
            if (baseMode != "w")
            {
                throw new ValueError("Cannot write to a tar archive opened for reading.");
            }
        }
    }
}
