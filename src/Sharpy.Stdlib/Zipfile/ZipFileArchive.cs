using System;
using System.IO;
using System.IO.Compression;

namespace Sharpy
{
    /// <summary>Represents a ZIP archive for reading, writing, and extracting members.</summary>
    [SharpyModuleType("zipfile", "ZipFile")]
    public partial class ZipFileArchive : IDisposable
    {
        private ZipArchive? _archive;
        private FileStream? _stream;
        private readonly string _mode;
        private readonly int _compression;
        private bool _closed;

        /// <summary>Opens a ZIP archive from a file path using the requested mode.</summary>
        public ZipFileArchive(string file, string mode = "r", int compression = 8, bool allowZip64 = true)
        {
            _mode = mode;
            _compression = compression;
            _closed = false;

            ZipArchiveMode archiveMode;
            switch (mode)
            {
                case "r":
                    archiveMode = ZipArchiveMode.Read;
                    try
                    {
                        _stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                    }
                    catch (FileNotFoundException)
                    {
                        throw new OSError("No such file or directory: '" + file + "'");
                    }
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
                    throw new ValueError("Bad mode: '" + mode + "'");
            }

            try
            {
                _archive = new ZipArchive(_stream, archiveMode, leaveOpen: false);
            }
            catch (InvalidDataException)
            {
                _stream.Dispose();
                throw new BadZipFile("File is not a zip file");
            }
        }

        /// <summary>Returns the names of all archive members.</summary>
        public List<string> Namelist()
        {
            EnsureOpen();
            var names = new List<string>();
            foreach (var entry in _archive!.Entries)
            {
                names.Append(entry.FullName);
            }
            return names;
        }

        /// <summary>Returns ZipInfo objects for all archive members.</summary>
        public List<ZipInfo> Infolist()
        {
            EnsureOpen();
            var infos = new List<ZipInfo>();
            foreach (var entry in _archive!.Entries)
            {
                infos.Append(ZipInfo.FromEntry(entry));
            }
            return infos;
        }

        /// <summary>Returns metadata for a named archive member.</summary>
        public ZipInfo Getinfo(string name)
        {
            EnsureOpen();
            var entry = _archive!.GetEntry(name);
            if (entry == null)
            {
                throw new KeyError("There is no item named '" + name + "' in the archive");
            }
            return ZipInfo.FromEntry(entry);
        }

        /// <summary>Reads an archive member and returns its bytes.</summary>
        public Bytes Read(string name)
        {
            EnsureOpen();
            if (_mode == "w")
            {
                throw new OSError("Cannot read from a ZipFile opened for writing");
            }

            var entry = _archive!.GetEntry(name);
            if (entry == null)
            {
                throw new KeyError("There is no item named '" + name + "' in the archive");
            }

            using (var stream = entry.Open())
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return new Bytes(ms.ToArray());
            }
        }

        /// <summary>Opens a stream for a named archive member.</summary>
        public Stream Open(string name, string mode = "r")
        {
            EnsureOpen();
            var entry = _archive!.GetEntry(name);
            if (entry == null)
            {
                throw new KeyError("There is no item named '" + name + "' in the archive");
            }
            return entry.Open();
        }

        /// <summary>Closes the archive and releases its underlying resources.</summary>
        public void Close()
        {
            if (!_closed)
            {
                _closed = true;
                if (_archive != null)
                {
                    _archive.Dispose();
                    _archive = null;
                }
                _stream = null;
            }
        }

        /// <summary>Disposes the archive and suppresses finalization.</summary>
        public void Dispose()
        {
            Close();
            GC.SuppressFinalize(this);
        }

        private void EnsureOpen()
        {
            if (_closed)
            {
                throw new ValueError("Attempt to use a closed ZipFile");
            }
        }

        private void EnsureWritable()
        {
            if (_mode == "r")
            {
                throw new OSError("Cannot write to a ZipFile opened for reading");
            }
        }

        private CompressionLevel GetCompressionLevel()
        {
            return _compression == 0
                ? CompressionLevel.NoCompression
                : CompressionLevel.Optimal;
        }
    }
}
