using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using SysPath = System.IO.Path;
using System.IO.Compression;

namespace Sharpy
{
    /// <summary>
    /// Tar archive for reading or writing.
    /// Equivalent to Python's <c>tarfile.TarFile</c>.
    /// </summary>
    [SharpyModuleType("tarfile", "TarFile")]
    public sealed class TarFile : IDisposable
    {
        private readonly string _mode;
        private readonly string _name;
        private readonly string _compression;
        private Stream? _baseStream;
        private TarWriter? _writer;
        private bool _disposed;
        private System.Collections.Generic.List<TarInfo>? _cachedMembers;

        internal TarFile(string name, string mode)
        {
            _name = name;
            _mode = mode;

            int colonIndex = mode.IndexOf(':');
            string baseMode = colonIndex >= 0 ? mode.Substring(0, colonIndex) : mode;
            _compression = colonIndex >= 0 ? mode.Substring(colonIndex + 1) : "";

            ValidateCompression(_compression);

            if (baseMode == "r")
            {
                // python opens the archive here (`open(name, "rb")`); its refusal is the classifier's.
                PathErrors.CheckNeedsFileToRead(name);
            }
            else if (baseMode == "w")
            {
                PathErrors.CheckNeedsFileToWrite(name);
                try
                {
                    _baseStream = new FileStream(name, FileMode.Create, FileAccess.Write, FileShare.None);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    throw PathErrors.TranslateNeedsFileToWrite(ex, name);
                }
                Stream writeStream = WrapCompressionStream(_baseStream, _compression);
                _writer = new TarWriter(writeStream, leaveOpen: false);
            }
            else
            {
                throw new ValueError("mode '" + mode + "' is not valid");
            }
        }

        public string Name => _name;

        public List<string> Getnames()
        {
            EnsureNotDisposed();
            EnsureReadMode();
            var result = new List<string>();
            foreach (var info in Getmembers())
            {
                result.Append(info.Name);
            }
            return result;
        }

        public List<TarInfo> Getmembers()
        {
            EnsureNotDisposed();
            EnsureReadMode();
            if (_cachedMembers != null)
            {
                return new List<TarInfo>(_cachedMembers);
            }

            var members = new System.Collections.Generic.List<TarInfo>();
            using var stream = OpenReadStream();
            using var reader = new TarReader(stream, leaveOpen: false);
            TarEntry? entry;
            while ((entry = reader.GetNextEntry()) != null)
            {
                members.Add(TarInfo.FromTarEntry(entry));
            }
            _cachedMembers = members;
            return new List<TarInfo>(members);
        }

        public TarInfo Getmember(string name)
        {
            foreach (var info in Getmembers())
            {
                if (info.Name == name || info.Name.TrimEnd('/') == name)
                {
                    return info;
                }
            }
            throw new KeyError("filename " + Builtins.Repr(name) + " not found");
        }

        public Bytes? Extractfile(string name)
        {
            EnsureNotDisposed();
            EnsureReadMode();

            using var stream = OpenReadStream();
            using var reader = new TarReader(stream, leaveOpen: false);
            TarEntry? entry;
            while ((entry = reader.GetNextEntry()) != null)
            {
                if (entry.Name == name || entry.Name.TrimEnd('/') == name)
                {
                    if (entry.EntryType == TarEntryType.Directory)
                    {
                        return null;
                    }
                    if (entry.DataStream != null)
                    {
                        using var ms = new MemoryStream();
                        entry.DataStream.CopyTo(ms);
                        return new Bytes(ms.ToArray());
                    }
                    return new Bytes(Array.Empty<byte>());
                }
            }
            throw new KeyError("filename " + Builtins.Repr(name) + " not found");
        }

        public void Extractall(string? path = null, List<TarInfo>? members = null)
        {
            EnsureNotDisposed();
            EnsureReadMode();
            string targetDir = path ?? ".";

            var allowedNames = members != null
                ? new System.Collections.Generic.HashSet<string>()
                : null;

            if (members != null)
            {
                foreach (var m in members)
                {
                    allowedNames!.Add(m.Name);
                }
            }

            using var stream = OpenReadStream();
            using var reader = new TarReader(stream, leaveOpen: false);
            TarEntry? entry;
            while ((entry = reader.GetNextEntry()) != null)
            {
                if (allowedNames != null && !allowedNames.Contains(entry.Name))
                {
                    continue;
                }
                // python's default errorlevel (1) raises a member's OSError as it is.
                ExtractEntry(entry, targetDir);
            }
        }

        public void Extract(string name, string? path = null)
        {
            EnsureNotDisposed();
            EnsureReadMode();
            string targetDir = path ?? ".";

            using var stream = OpenReadStream();
            using var reader = new TarReader(stream, leaveOpen: false);
            TarEntry? entry;
            while ((entry = reader.GetNextEntry()) != null)
            {
                if (entry.Name == name || entry.Name.TrimEnd('/') == name)
                {
                    ExtractEntry(entry, targetDir);
                    return;
                }
            }
            throw new KeyError("filename " + Builtins.Repr(name) + " not found");
        }

        public void Add(string name, string? arcname = null, bool recursive = true)
        {
            EnsureNotDisposed();
            EnsureWriteMode();
            string archiveName = arcname ?? name;

            // python stats the member first (`os.lstat(name)`); its refusal is the classifier's.
            PathErrors.CheckNeedsAny(name);
            try
            {
                if (Directory.Exists(name))
                {
                    if (recursive)
                    {
                        foreach (string file in Directory.GetFiles(name, "*", SearchOption.AllDirectories))
                        {
                            string relativePath = SysPath.GetRelativePath(name, file);
                            string entryName = string.IsNullOrEmpty(archiveName)
                                ? relativePath
                                : SysPath.Combine(archiveName, relativePath);
                            entryName = entryName.Replace('\\', '/');
                            _writer!.WriteEntry(file, entryName);
                        }
                    }
                }
                else
                {
                    _writer!.WriteEntry(name, archiveName);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw PathErrors.TranslateNeedsAny(ex, name);
            }
        }

        public void Addfile(TarInfo tarinfo, System.IO.Stream? fileobj = null)
        {
            EnsureNotDisposed();
            EnsureWriteMode();

            TarEntryType entryType;
            if (tarinfo.Type == TarfileModule.DIRTYPE)
                entryType = TarEntryType.Directory;
            else if (tarinfo.Type == TarfileModule.SYMTYPE)
                entryType = TarEntryType.SymbolicLink;
            else if (tarinfo.Type == TarfileModule.LNKTYPE)
                entryType = TarEntryType.HardLink;
            else
                entryType = TarEntryType.RegularFile;

            var entry = new PaxTarEntry(entryType, tarinfo.Name)
            {
                ModificationTime = DateTimeOffset.FromUnixTimeSeconds((long)tarinfo.Mtime),
            };

            if (tarinfo.Linkname.Length > 0)
            {
                entry.LinkName = tarinfo.Linkname;
            }

            if (fileobj != null && entryType == TarEntryType.RegularFile)
            {
                entry.DataStream = fileobj;
            }

            _writer!.WriteEntry(entry);
        }

        public void Close()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            _writer?.Dispose();
            _writer = null;

            _baseStream?.Dispose();
            _baseStream = null;
        }

        public override string ToString() => "<TarFile '" + _name + "'>";

        private Stream OpenReadStream()
        {
            FileStream fileStream;
            try
            {
                fileStream = new FileStream(_name, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw PathErrors.TranslateNeedsFileToRead(ex, _name);
            }
            return WrapDecompressionStream(fileStream, _compression);
        }

        private static void ExtractEntry(TarEntry entry, string targetDir)
        {
            string entryPath = entry.Name;
            if (entryPath.Contains(".."))
            {
                throw new ValueError("Tar entry name contains '..': '" + entryPath + "'");
            }

            string fullPath = SysPath.GetFullPath(SysPath.Combine(targetDir, entryPath));
            string fullTargetDir = SysPath.GetFullPath(targetDir);
            if (!fullPath.StartsWith(fullTargetDir, StringComparison.Ordinal))
            {
                throw new ValueError("Tar entry would extract outside target directory: '" + entryPath + "'");
            }

            // python's _extract_member: the upper directories are made only when missing, then the
            // member is created; every refusal is the classifier's (plan-638e8b D1).
            fullPath = fullPath.TrimEnd(SysPath.DirectorySeparatorChar, SysPath.AltDirectorySeparatorChar);
            MakeUpperDirs(SysPath.GetDirectoryName(fullPath));

            if (entry.EntryType == TarEntryType.Directory)
            {
                // python's makedir: os.mkdir, an existing directory accepted.
                if (!Directory.Exists(fullPath))
                {
                    PathErrors.CheckCreatesDir(fullPath);
                    try
                    {
                        Directory.CreateDirectory(fullPath);
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                    {
                        throw PathErrors.TranslateCreatesDir(ex, fullPath);
                    }
                }
            }
            else if (entry.EntryType == TarEntryType.RegularFile || entry.EntryType == TarEntryType.V7RegularFile)
            {
                // python's makefile: open(targetpath, "wb").
                PathErrors.CheckNeedsFileToWrite(fullPath);
                try
                {
                    entry.ExtractToFile(fullPath, overwrite: true);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    throw PathErrors.TranslateNeedsFileToWrite(ex, fullPath);
                }
            }
            else if (entry.EntryType == TarEntryType.SymbolicLink && entry.LinkName != null)
            {
                try
                {
                    File.CreateSymbolicLink(fullPath, entry.LinkName);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    throw PathErrors.TranslateCreatesExclusively(ex, fullPath);
                }
            }
        }

        /// <summary>python's <c>if upperdirs and not os.path.exists(upperdirs): os.makedirs(upperdirs)</c>.</summary>
        private static void MakeUpperDirs(string? upperDirs)
        {
            if (string.IsNullOrEmpty(upperDirs) || Directory.Exists(upperDirs) || File.Exists(upperDirs))
            {
                return;
            }
            try
            {
                Directory.CreateDirectory(upperDirs!);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw PathErrors.TranslateCreatesDir(ex, upperDirs!);
            }
        }

        private static Stream WrapDecompressionStream(Stream baseStream, string compression)
        {
            switch (compression)
            {
                case "gz":
                    return new GZipStream(baseStream, CompressionMode.Decompress);
                case "bz2":
                case "xz":
                    baseStream.Dispose();
                    throw new CompressionError(compression + " compression not yet supported");
                case "":
                    return baseStream;
                default:
                    baseStream.Dispose();
                    throw new CompressionError("unsupported compression: '" + compression + "'");
            }
        }

        private static Stream WrapCompressionStream(Stream baseStream, string compression)
        {
            switch (compression)
            {
                case "gz":
                    return new GZipStream(baseStream, CompressionMode.Compress, leaveOpen: true);
                case "bz2":
                case "xz":
                    baseStream.Dispose();
                    throw new CompressionError(compression + " compression not yet supported");
                case "":
                    return baseStream;
                default:
                    baseStream.Dispose();
                    throw new CompressionError("unsupported compression: '" + compression + "'");
            }
        }

        private static void ValidateCompression(string compression)
        {
            switch (compression)
            {
                case "":
                case "gz":
                    return;
                case "bz2":
                case "xz":
                    throw new CompressionError(compression + " compression not yet supported");
                default:
                    throw new CompressionError("unsupported compression: '" + compression + "'");
            }
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
                throw new ValueError("I/O operation on closed tar archive");
        }

        private void EnsureReadMode()
        {
            int colonIndex = _mode.IndexOf(':');
            string baseMode = colonIndex >= 0 ? _mode.Substring(0, colonIndex) : _mode;
            if (baseMode != "r")
            {
                throw new ValueError("Cannot read from a tar archive opened for writing");
            }
        }

        private void EnsureWriteMode()
        {
            int colonIndex = _mode.IndexOf(':');
            string baseMode = colonIndex >= 0 ? _mode.Substring(0, colonIndex) : _mode;
            if (baseMode != "w")
            {
                throw new ValueError("Cannot write to a tar archive opened for reading");
            }
        }
    }
}
