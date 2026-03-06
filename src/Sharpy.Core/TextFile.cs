using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// A file object wrapping StreamReader/StreamWriter that provides a Python-like file API.
    /// Implements IDisposable for use with Sharpy's <c>with</c> statement (C# <c>using</c>).
    /// </summary>
    public class TextFile : IDisposable
    {
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private bool _closed;
        private readonly string _path;
        private readonly string _mode;

        internal TextFile(string path, string mode, Encoding encoding)
        {
            _path = path;
            _mode = mode;

            switch (mode)
            {
                case "r":
                    if (Directory.Exists(path))
                        throw new IsADirectoryError("Is a directory: '" + path + "'");
                    if (!File.Exists(path))
                        throw new FileNotFoundError("No such file or directory: '" + path + "'");
                    try
                    {
                        _reader = new StreamReader(path, encoding);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        throw new PermissionError("Permission denied: '" + path + "'", ex);
                    }
                    break;
                case "w":
                    if (Directory.Exists(path))
                        throw new IsADirectoryError("Is a directory: '" + path + "'");
                    try
                    {
                        _writer = new StreamWriter(path, false, encoding);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        throw new PermissionError("Permission denied: '" + path + "'", ex);
                    }
                    break;
                case "a":
                    if (Directory.Exists(path))
                        throw new IsADirectoryError("Is a directory: '" + path + "'");
                    try
                    {
                        _writer = new StreamWriter(path, true, encoding);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        throw new PermissionError("Permission denied: '" + path + "'", ex);
                    }
                    break;
                case "x":
                    if (File.Exists(path))
                        throw new FileExistsError("File exists: '" + path + "'");
                    if (Directory.Exists(path))
                        throw new IsADirectoryError("Is a directory: '" + path + "'");
                    try
                    {
                        var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
                        _writer = new StreamWriter(stream, encoding);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        throw new PermissionError("Permission denied: '" + path + "'", ex);
                    }
                    break;
                default:
                    throw new ValueError("invalid mode: '" + mode + "'");
            }
        }

        /// <summary>Whether the file is closed.</summary>
        public bool Closed => _closed;

        /// <summary>The file path.</summary>
        public string Name => _path;

        /// <summary>The mode string.</summary>
        public string Mode => _mode;

        private bool IsReadMode => _mode == "r";

        private void EnsureNotClosed()
        {
            if (_closed)
                throw new ValueError("I/O operation on closed file.");
        }

        private void EnsureReadable()
        {
            EnsureNotClosed();
            if (!IsReadMode)
                throw new ValueError("not readable");
        }

        private void EnsureWritable()
        {
            EnsureNotClosed();
            if (IsReadMode)
                throw new ValueError("not writable");
        }

        /// <summary>Read the entire remaining content of the file.</summary>
        public string Read()
        {
            EnsureReadable();
            return _reader!.ReadToEnd();
        }

        /// <summary>Read at most <paramref name="size"/> characters from the file.</summary>
        public string Read(int size)
        {
            EnsureReadable();
            if (size < 0)
                return _reader!.ReadToEnd();
            var buffer = new char[size];
            int read = _reader!.Read(buffer, 0, size);
            return new string(buffer, 0, read);
        }

        /// <summary>
        /// Read one line from the file, including the trailing newline if present.
        /// Returns an empty string at EOF.
        /// </summary>
        public string Readline()
        {
            EnsureReadable();
            var sb = new StringBuilder();
            int ch;
            while ((ch = _reader!.Read()) != -1)
            {
                sb.Append((char)ch);
                if (ch == '\n')
                    break;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Read all remaining lines from the file. Each line includes its trailing newline if present.
        /// </summary>
        public List<string> Readlines()
        {
            EnsureReadable();
            var lines = new List<string>();
            string line;
            while ((line = Readline()).Length > 0)
            {
                lines.Add(line);
            }
            return lines;
        }

        /// <summary>Write a string to the file. Returns the number of characters written.</summary>
        public int Write(string s)
        {
            EnsureWritable();
            _writer!.Write(s);
            return s.Length;
        }

        /// <summary>Write an iterable of strings to the file. No separator is added.</summary>
        public void Writelines(IEnumerable<string> lines)
        {
            EnsureWritable();
            foreach (var line in lines)
            {
                _writer!.Write(line);
            }
        }

        /// <summary>Flush the write buffer.</summary>
        public void Flush()
        {
            EnsureWritable();
            _writer!.Flush();
        }

        /// <summary>Seek to a position in the file (readable files only).</summary>
        public void Seek(long offset)
        {
            EnsureNotClosed();
            if (IsReadMode)
            {
                _reader!.DiscardBufferedData();
                _reader.BaseStream.Seek(offset, SeekOrigin.Begin);
            }
            else
            {
                _writer!.Flush();
                _writer.BaseStream.Seek(offset, SeekOrigin.Begin);
            }
        }

        /// <summary>Return the current stream position.</summary>
        public long Tell()
        {
            EnsureNotClosed();
            if (IsReadMode)
                return _reader!.BaseStream.Position;
            else
            {
                _writer!.Flush();
                return _writer.BaseStream.Position;
            }
        }

        /// <summary>Close the file.</summary>
        public void Close()
        {
            if (_closed)
                return;
            _closed = true;
            _reader?.Dispose();
            _writer?.Dispose();
            _reader = null;
            _writer = null;
        }

        /// <summary>Dispose the file (calls Close).</summary>
        public void Dispose()
        {
            Close();
        }
    }
}
