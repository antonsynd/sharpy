using System;
using System.IO;
using System.IO.Compression;

namespace Sharpy
{
    [SharpyModuleType("gzip")]
    public class GzipFile : IDisposable
    {
        private readonly string _name;
        private readonly int _mode;
        private Stream? _stream;
        private GZipStream? _gzipStream;
        private bool _closed;

        public GzipFile(string filename = "", string mode = "rb", int compresslevel = 9, Stream? fileobj = null)
        {
            _name = filename;
            _closed = false;

            Stream baseStream;
            if (fileobj != null)
            {
                baseStream = fileobj;
            }
            else if (string.IsNullOrEmpty(filename))
            {
                throw new ValueError("Either filename or fileobj must be provided");
            }
            else
            {
                baseStream = OpenFileStream(filename, mode);
            }

            _stream = baseStream;

            switch (mode)
            {
                case "rb":
                    _mode = 1;
                    _gzipStream = new GZipStream(baseStream, CompressionMode.Decompress, leaveOpen: fileobj != null);
                    break;
                case "wb":
                    _mode = 2;
                    CompressionLevel level = GzipModule.MapGzipLevel(compresslevel);
                    _gzipStream = new GZipStream(baseStream, level, leaveOpen: fileobj != null);
                    break;
                case "ab":
                    _mode = 2;
                    CompressionLevel appendLevel = GzipModule.MapGzipLevel(compresslevel);
                    _gzipStream = new GZipStream(baseStream, appendLevel, leaveOpen: fileobj != null);
                    break;
                default:
                    throw new ValueError("Invalid mode: '" + mode + "'. Use 'rb', 'wb', or 'ab'.");
            }
        }

        public string Name => _name;

        public int Mode => _mode;

        public Bytes Read(int size = -1)
        {
            EnsureOpen();
            if (_mode != 1)
            {
                throw new OSError("read() on write-only GzipFile");
            }

            try
            {
                if (size < 0)
                {
                    using (var output = new MemoryStream())
                    {
                        _gzipStream!.CopyTo(output);
                        return new Bytes(output.ToArray());
                    }
                }

                byte[] buffer = new byte[size];
                int totalRead = 0;
                while (totalRead < size)
                {
                    int read = _gzipStream!.Read(buffer, totalRead, size - totalRead);
                    if (read == 0)
                    {
                        break;
                    }
                    totalRead += read;
                }

                if (totalRead < size)
                {
                    byte[] result = new byte[totalRead];
                    Array.Copy(buffer, result, totalRead);
                    return new Bytes(result);
                }

                return new Bytes(buffer);
            }
            catch (InvalidDataException ex)
            {
                throw new BadGzipFile("Not a gzipped file (" + ex.Message + ")");
            }
        }

        public int Write(Bytes data)
        {
            EnsureOpen();
            if (_mode != 2)
            {
                throw new OSError("write() on read-only GzipFile");
            }

            byte[] bytes = data.ToArray();
            _gzipStream!.Write(bytes, 0, bytes.Length);
            return bytes.Length;
        }

        public void Close()
        {
            if (!_closed)
            {
                _closed = true;
                if (_gzipStream != null)
                {
                    _gzipStream.Dispose();
                    _gzipStream = null;
                }
                if (_stream != null)
                {
                    _stream.Dispose();
                    _stream = null;
                }
            }
        }

        public bool Readable() => _mode == 1;

        public bool Writable() => _mode == 2;

        public bool Seekable() => false;

        public void Dispose()
        {
            Close();
        }

        private void EnsureOpen()
        {
            if (_closed)
            {
                throw new ValueError("I/O operation on closed file");
            }
        }

        private static FileStream OpenFileStream(string filename, string mode)
        {
            switch (mode)
            {
                case "rb":
                    return new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                case "wb":
                    return new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None);
                case "ab":
                    return new FileStream(filename, FileMode.Append, FileAccess.Write, FileShare.None);
                default:
                    throw new ValueError("Invalid mode: '" + mode + "'");
            }
        }
    }
}
